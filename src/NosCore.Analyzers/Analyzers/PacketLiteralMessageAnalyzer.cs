using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PacketLiteralMessageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCorePacketLiteralMessage";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not send a player-facing literal in a packet that has an i18n variant",
            "'{0}.{1}' is assigned a string literal; send '{2}' with a Game18NConstString instead",
            "Globalization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Player-facing text must be localized. When the client has an i18n twin of a packet (for example MsgiPacket for MsgPacket), send that packet with a Game18NConstString instead of a literal.");

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
            if (property == null || property.Type.SpecialType != SpecialType.System_String)
            {
                return;
            }

            if (PacketTypes.Unwrap(assignment.Value) is not ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } || text.Trim().Length == 0)
            {
                return;
            }

            var packet = property.ContainingType;
            var twin = I18NTwin(packet);
            if (twin == null)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Value.Syntax.GetLocation(), packet.Name, property.Name, twin.Name));
        }

        private static INamedTypeSymbol? I18NTwin(INamedTypeSymbol packet)
        {
            const string suffix = "Packet";
            if (!packet.Name.EndsWith(suffix) || packet.ContainingNamespace == null)
            {
                return null;
            }

            var twinName = packet.Name.Substring(0, packet.Name.Length - suffix.Length) + "i" + suffix;
            return packet.ContainingNamespace.GetTypeMembers(twinName).FirstOrDefault(PacketTypes.IsPacket);
        }
    }
}
