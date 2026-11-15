using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LogLiteralAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreLogLiteral";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not log written text",
            "'{0}' logs text written in code; add a LogLanguageKey and read it through the localizer",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Log messages live in LocalizedResources.resx under a LogLanguageKey so they can be translated, never as literals in code.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var extensions = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions");
                if (extensions == null)
                {
                    return;
                }

                start.RegisterOperationAction(operation => Analyze(operation, extensions), OperationKind.Invocation);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol extensions)
        {
            var invocation = (IInvocationOperation)context.Operation;
            if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, extensions))
            {
                return;
            }

            var message = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "message");
            if (message != null && TextHelper.IsWrittenText(message.Value))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, message.Syntax.GetLocation(), invocation.TargetMethod.Name));
            }
        }
    }
}
