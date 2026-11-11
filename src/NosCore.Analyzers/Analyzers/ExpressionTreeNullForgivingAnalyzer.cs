using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExpressionTreeNullForgivingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreExpressionTreeNullForgiving";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not use '!' inside an EF query",
            "'!' does nothing inside an EF query; remove it",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Nullable dereference warnings are suppressed inside lambdas passed to Queryable, EF Core or IDao as expression trees, so a null-forgiving operator before a member access there is redundant.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var efQuery = new EfQueryLambda(start.Compilation);
                start.RegisterSyntaxNodeAction(node => Analyze(node, efQuery), SyntaxKind.SuppressNullableWarningExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context, EfQueryLambda efQuery)
        {
            var forgiving = (PostfixUnaryExpressionSyntax)context.Node;
            var isReceiver = forgiving.Parent switch
            {
                MemberAccessExpressionSyntax access => access.Expression == forgiving,
                ElementAccessExpressionSyntax access => access.Expression == forgiving,
                _ => false,
            };
            if (!isReceiver || !efQuery.IsInside(forgiving, context.SemanticModel))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, forgiving.OperatorToken.GetLocation()));
        }
    }
}
