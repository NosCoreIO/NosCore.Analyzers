using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    /// <summary>
    /// An EF query lambda is translated to SQL, not run: a member access on a null navigation becomes
    /// a null, so nullable dereference warnings there are noise. Other expression trees (Moq matchers,
    /// compiled expressions) do run, so they keep their warnings.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExpressionTreeNullableSuppressor : DiagnosticSuppressor
    {
        private static readonly ImmutableArray<SuppressionDescriptor> Suppressions = ImmutableArray.Create(
            Describe("CS8602"),
            Describe("CS8604"),
            Describe("CS8629"));

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => Suppressions;

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var efQuery = new EfQueryLambda(context.Compilation);
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var tree = diagnostic.Location.SourceTree;
                if (tree == null)
                {
                    continue;
                }

                var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                if (efQuery.IsInside(node, context.GetSemanticModel(tree)))
                {
                    context.ReportSuppression(Suppression.Create(Suppressions.First(s => s.SuppressedDiagnosticId == diagnostic.Id), diagnostic));
                }
            }
        }

        private static SuppressionDescriptor Describe(string diagnosticId) => new(
            "NosCoreExpressionTreeNullable" + diagnosticId,
            diagnosticId,
            "EF translates query lambdas to SQL rather than running them, so a null along a member chain cannot throw.");
    }
}
