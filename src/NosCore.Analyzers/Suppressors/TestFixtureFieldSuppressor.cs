using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Suppressors
{
    /// <summary>
    /// MSTest builds a new test class instance for every test, and its [TestInitialize] method and the test's own
    /// steps assign the fixture's members before reading them. A member of a test class that one of the class's
    /// methods assigns is therefore never observed uninitialized; a test that reads it too early fails on its own.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TestFixtureFieldSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor Descriptor = new(
            "NosCoreTestFixture8618",
            "CS8618",
            "The member belongs to a test class and is assigned by one of its methods before a test reads it.");

        private static readonly string[] TestAttributes =
        {
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        };

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Descriptor);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var testAttributes = TestAttributes
                .Select(context.Compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            if (testAttributes.IsEmpty)
            {
                return;
            }

            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var member = UninitializedMember.Find(diagnostic, context);
                if (member == null || member.IsStatic || !IsTestClass(member.ContainingType, testAttributes))
                {
                    continue;
                }

                if (IsAssignedByAMethod(member, context))
                {
                    context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
                }
            }
        }

        private static bool IsTestClass(INamedTypeSymbol type, ImmutableHashSet<INamedTypeSymbol> testAttributes)
        {
            bool HasTestAttribute(ISymbol symbol) =>
                symbol.GetAttributes().Any(a => a.AttributeClass != null && testAttributes.Contains(a.AttributeClass));

            for (var current = type; current != null; current = current.BaseType)
            {
                if (HasTestAttribute(current) || current.GetMembers().OfType<IMethodSymbol>().Any(HasTestAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAssignedByAMethod(ISymbol member, SuppressionAnalysisContext context)
        {
            foreach (var reference in member.ContainingType.DeclaringSyntaxReferences)
            {
                var declaration = reference.GetSyntax(context.CancellationToken);
                var model = context.GetSemanticModel(declaration.SyntaxTree);
                var targets = declaration.DescendantNodes()
                    .Where(node => node.Ancestors().Any(a => a is MethodDeclarationSyntax or AccessorDeclarationSyntax))
                    .Select(node => node switch
                    {
                        AssignmentExpressionSyntax assignment => assignment.Left,
                        ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) => argument.Expression,
                        _ => null,
                    })
                    .OfType<ExpressionSyntax>();
                if (targets.Any(target => SymbolEqualityComparer.Default.Equals(
                        model.GetSymbolInfo(target, context.CancellationToken).Symbol, member)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
