using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IgnoredDaoResultAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreIgnoredDaoResult";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Check the result of a DAO write",
            "The result of '{0}' is ignored; a failed write would go unnoticed",
            "Reliability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The DAO Try methods report a failed write through their result instead of throwing. Dropping the result leaves the caller carrying on as if the row were written. Discard it with '_ =' only where a failure really does not matter.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var dao = start.Compilation.GetTypeByMetadataName("NosCore.Dao.Interfaces.IDao`2");
                if (dao == null)
                {
                    return;
                }

                start.RegisterOperationAction(operation => Analyze(operation, dao), OperationKind.ExpressionStatement);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol dao)
        {
            var expression = ((IExpressionStatementOperation)context.Operation).Operation;
            if (expression is IAwaitOperation awaited)
            {
                expression = awaited.Operation;
            }

            while (expression is IConversionOperation conversion)
            {
                expression = conversion.Operand;
            }

            if (expression is not IInvocationOperation invocation || !invocation.TargetMethod.Name.StartsWith("Try"))
            {
                return;
            }

            if (!IsDaoMethod(invocation.TargetMethod, invocation.TargetMethod.ContainingType, dao)
                && !(invocation.Instance?.Type is INamedTypeSymbol receiver && IsDaoMethod(invocation.TargetMethod, receiver, dao)))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), invocation.TargetMethod.Name));
        }

        // The receiver counts too: a class can take its IDao methods from a base class that does
        // not implement the interface itself.
        private static bool IsDaoMethod(IMethodSymbol method, INamedTypeSymbol type, INamedTypeSymbol dao)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, dao))
            {
                return true;
            }

            foreach (var implemented in type.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, dao))
                {
                    continue;
                }

                foreach (var member in implemented.GetMembers(method.Name))
                {
                    if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), method.OriginalDefinition)
                        || SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), method))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
