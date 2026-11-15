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
            description: "Code that reads the clock through DateTime, DateTimeOffset or NodaTime's SystemClock cannot be tested with a fake clock.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var dateTime = start.Compilation.GetTypeByMetadataName("System.DateTime");
                var dateTimeOffset = start.Compilation.GetTypeByMetadataName("System.DateTimeOffset");
                var systemClock = start.Compilation.GetTypeByMetadataName("NodaTime.SystemClock");
                start.RegisterOperationAction(operation => Analyze(operation, dateTime, dateTimeOffset, systemClock), OperationKind.PropertyReference);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol? dateTime, INamedTypeSymbol? dateTimeOffset,
            INamedTypeSymbol? systemClock)
        {
            for (var parent = context.Operation.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is INameOfOperation)
                {
                    return;
                }
            }

            var property = ((IPropertyReferenceOperation)context.Operation).Property;
            if (!property.IsStatic)
            {
                return;
            }

            var type = property.ContainingType;
            var readsTheClock = SymbolEqualityComparer.Default.Equals(type, systemClock)
                ? property.Name == "Instance"
                : (SymbolEqualityComparer.Default.Equals(type, dateTime) || SymbolEqualityComparer.Default.Equals(type, dateTimeOffset))
                  && property.Name is "Now" or "UtcNow" or "Today";
            if (!readsTheClock)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), type.Name, property.Name));
        }
    }
}
