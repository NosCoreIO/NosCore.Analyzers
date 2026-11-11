using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullForgivingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreNullForgiving";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not use the null-forgiving operator",
            "The null-forgiving operator hides a possible null; make the type say whether it can be null instead",
            "Reliability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "'!' silences the compiler without checking anything, so a null slips through to the next dereference. Check the value, change the declared type, or rely on a NosCore suppressor where the warning is a known false positive.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                analysis => analysis.ReportDiagnostic(Diagnostic.Create(Rule, analysis.Node.GetLastToken().GetLocation())),
                SyntaxKind.SuppressNullableWarningExpression);
        }
    }
}
