using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SharedRandomAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreSharedRandom";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not roll on a random the caller cannot replace",
            "'{0}' rolls on a random a test cannot script; inject the random source instead",
            "Reliability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Odds read from Random.Shared, a new Random or RandomHelper.Instance cannot be fixed by a test, so neither outcome can be checked.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var random = start.Compilation.GetTypeByMetadataName("System.Random");
                var randomHelper = start.Compilation.GetTypeByMetadataName("NosCore.Shared.Helpers.RandomHelper");
                start.RegisterOperationAction(operation => AnalyzeProperty(operation, random, randomHelper), OperationKind.PropertyReference);
                start.RegisterOperationAction(operation => AnalyzeCreation(operation, random), OperationKind.ObjectCreation);
            });
        }

        private static void AnalyzeProperty(OperationAnalysisContext context, INamedTypeSymbol? random, INamedTypeSymbol? randomHelper)
        {
            if (IsInNameOf(context.Operation))
            {
                return;
            }

            var property = ((IPropertyReferenceOperation)context.Operation).Property;
            if (!property.IsStatic)
            {
                return;
            }

            var type = property.ContainingType;
            var shared = (SymbolEqualityComparer.Default.Equals(type, random) && property.Name == "Shared")
                || (SymbolEqualityComparer.Default.Equals(type, randomHelper) && property.Name == "Instance");
            if (shared)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), $"{type.Name}.{property.Name}"));
            }
        }

        private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol? random)
        {
            var creation = (IObjectCreationOperation)context.Operation;
            if (random != null && SymbolEqualityComparer.Default.Equals(creation.Type, random))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), "new Random"));
            }
        }

        private static bool IsInNameOf(IOperation operation)
        {
            for (var parent = operation.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is INameOfOperation)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
