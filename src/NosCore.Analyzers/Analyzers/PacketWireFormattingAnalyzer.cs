using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PacketWireFormattingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCorePacketWireFormat";

        private const string Separators = " ^|.";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not hand-build the wire format of a packet property",
            "'{0}' is assigned a value formatted for the wire ({1}); let the NosCore.Packets serializer own the format",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The serializer owns the wire format. Escaping spaces with '^' or joining values with a packet separator belongs on the packet definition in NosCore.Packets, not at the assignment.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(Analyze, OperationKind.SimpleAssignment);
        }

        private static void Analyze(OperationAnalysisContext context)
        {
            var assignment = (ISimpleAssignmentOperation)context.Operation;
            var property = PacketTypes.PacketPropertyTarget(assignment);
            if (property == null)
            {
                return;
            }

            var reason = Describe(PacketTypes.Unwrap(assignment.Value));
            if (reason != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Value.Syntax.GetLocation(), property.Name, reason));
            }
        }

        private static string? Describe(IOperation value)
        {
            switch (value)
            {
                case IInvocationOperation invocation when invocation.TargetMethod.ContainingType.SpecialType == SpecialType.System_String:
                    var method = invocation.TargetMethod;
                    if (method.Name == "Replace" && invocation.Arguments.Length >= 2
                        && (IsSeparatorConstant(invocation.Arguments[0].Value) || IsSeparatorConstant(invocation.Arguments[1].Value)))
                    {
                        return "a separator replaced by hand";
                    }

                    if (method.Name == "Join" && invocation.Arguments.Length >= 1 && IsSeparatorConstant(invocation.Arguments[0].Value))
                    {
                        return "values joined with a packet separator";
                    }

                    return null;

                case IInterpolatedStringOperation interpolated:
                    var parts = interpolated.Parts;
                    for (var i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] is not IInterpolatedStringTextOperation text || text.Text.ConstantValue.Value is not string chunk)
                        {
                            continue;
                        }

                        var betweenValues = i > 0 && i < parts.Length - 1
                            && parts[i - 1] is IInterpolationOperation && parts[i + 1] is IInterpolationOperation;
                        if ((betweenValues && IsOnlySeparators(chunk)) || chunk.IndexOfAny(new[] { '^', '|' }) >= 0)
                        {
                            return "an interpolated string with a packet separator";
                        }
                    }

                    return null;

                case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary when binary.Type?.SpecialType == SpecialType.System_String:
                    return Concatenation(binary) ? "a concatenation with a packet separator" : null;

                default:
                    return null;
            }
        }

        private static bool Concatenation(IOperation operation)
        {
            operation = PacketTypes.Unwrap(operation);
            if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary)
            {
                return Concatenation(binary.LeftOperand) || Concatenation(binary.RightOperand);
            }

            return IsSeparatorConstant(operation);
        }

        private static bool IsSeparatorConstant(IOperation operation)
        {
            var constant = PacketTypes.Unwrap(operation).ConstantValue;
            if (!constant.HasValue)
            {
                return false;
            }

            return constant.Value switch
            {
                char c => Separators.IndexOf(c) >= 0,
                string s => IsOnlySeparators(s),
                _ => false,
            };
        }

        private static bool IsOnlySeparators(string value) => value.Length > 0 && value.All(c => Separators.IndexOf(c) >= 0);
    }
}
