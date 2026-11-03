using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DateTimeNowAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreDateTimeNow";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not read the system clock directly",
            "'{0}.{1}' reads the system clock; inject a NodaTime IClock instead",
            "Reliability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Code that reads the clock through DateTime or DateTimeOffset cannot be tested with a fake clock.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var dateTime = start.Compilation.GetTypeByMetadataName("System.DateTime");
                var dateTimeOffset = start.Compilation.GetTypeByMetadataName("System.DateTimeOffset");
                start.RegisterOperationAction(operation => Analyze(operation, dateTime, dateTimeOffset), OperationKind.PropertyReference);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol? dateTime, INamedTypeSymbol? dateTimeOffset)
        {
            var property = ((IPropertyReferenceOperation)context.Operation).Property;
            if (!property.IsStatic || property.Name is not ("Now" or "UtcNow" or "Today"))
            {
                return;
            }

            var type = property.ContainingType;
            if (!SymbolEqualityComparer.Default.Equals(type, dateTime) && !SymbolEqualityComparer.Default.Equals(type, dateTimeOffset))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), type.Name, property.Name));
        }
    }
}
