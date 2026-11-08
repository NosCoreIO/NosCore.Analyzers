using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConfigureAwaitAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreConfigureAwait";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not call ConfigureAwait",
            "Remove the 'ConfigureAwait' call; NosCore hosts have no synchronization context to return to",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Every NosCore host is a generic-host console app with no synchronization context, so ConfigureAwait only adds noise.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(Analyze, OperationKind.Invocation);
        }

        private static void Analyze(OperationAnalysisContext context)
        {
            var method = ((IInvocationOperation)context.Operation).TargetMethod;
            if (method.Name != "ConfigureAwait")
            {
                return;
            }

            var ns = method.ContainingNamespace?.ToDisplayString();
            if (ns is not ("System.Threading.Tasks" or "System.Runtime.CompilerServices"))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation()));
        }
    }
}
