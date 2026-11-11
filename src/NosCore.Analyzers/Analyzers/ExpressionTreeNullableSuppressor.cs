using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    /// <summary>
    /// A lambda compiled to an expression tree is translated, not run: EF Core turns a member access
    /// on a null navigation into a null in SQL, so nullable dereference warnings there are noise.
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
            var lambdaExpression = context.Compilation.GetTypeByMetadataName("System.Linq.Expressions.LambdaExpression");
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var tree = diagnostic.Location.SourceTree;
                if (tree == null)
                {
                    continue;
                }

                var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                if (ExpressionTreeLambda.IsInside(node, context.GetSemanticModel(tree), lambdaExpression))
                {
                    context.ReportSuppression(Suppression.Create(Suppressions.First(s => s.SuppressedDiagnosticId == diagnostic.Id), diagnostic));
                }
            }
        }

        private static SuppressionDescriptor Describe(string diagnosticId) => new(
            "NosCoreExpressionTreeNullable" + diagnosticId,
            diagnosticId,
            "Expression trees are translated rather than executed, so a null along a member chain cannot throw.");
    }
}
