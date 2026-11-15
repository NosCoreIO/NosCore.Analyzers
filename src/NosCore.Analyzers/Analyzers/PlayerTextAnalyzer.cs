using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PlayerTextAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCorePlayerText";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not send text written in code to the player",
            "'{0}.{1}' is given text written in code; send the client's own Game18NConstString through an i18n packet, or a LanguageKey",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The client already holds a translation of almost every line. Player-facing text comes from it or from a LanguageKey, never from a literal.");

        private static readonly ImmutableHashSet<string> TextProperties = ImmutableHashSet.Create("Message", "Question", "Title", "Body");

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
            if (assignment.Target is not IPropertyReferenceOperation { Property: var property }
                || !TextProperties.Contains(property.Name)
                || property.Type.SpecialType != SpecialType.System_String
                || property.ContainingType.ContainingNamespace?.ToDisplayString() is not { } ns
                || !ns.StartsWith("NosCore.Packets.ServerPackets", System.StringComparison.Ordinal)
                || !TextHelper.IsWrittenText(assignment.Value))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Value.Syntax.GetLocation(), property.ContainingType.Name, property.Name));
        }
    }
}
