using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WireEscapeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreWireEscape";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not escape spaces for the wire by hand",
            "Replacing spaces with '{0}' shapes a value for the wire; mark the packet property with EscapeSpaces in NosCore.Packets instead",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The serializer owns the wire format. A field whose spaces must be escaped says so on the packet property.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(Analyze, OperationKind.Invocation);
        }

        private static void Analyze(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (method.Name != "Replace" || method.ContainingType.SpecialType != SpecialType.System_String || invocation.Arguments.Length < 2)
            {
                return;
            }

            var from = ConstantOf(invocation, 0);
            var to = ConstantOf(invocation, 1);
            if (!from.HasValue || !to.HasValue || from.Value?.ToString() != " ")
            {
                return;
            }

            var escape = to.Value?.ToString();
            if (escape is "^" or "|")
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), escape));
            }
        }

        private static Optional<object?> ConstantOf(IInvocationOperation invocation, int ordinal)
        {
            var value = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;
            if (value == null)
            {
                return default;
            }

            return value.ConstantValue.HasValue ? value.ConstantValue : TextHelper.Unwrap(value).ConstantValue;
        }
    }
}
