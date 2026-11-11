using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NosCore.Analyzers.Analyzers
{
    internal static class ExpressionTreeLambda
    {
        public static bool IsInside(SyntaxNode node, SemanticModel model, INamedTypeSymbol? lambdaExpression)
        {
            if (lambdaExpression == null)
            {
                return false;
            }

            for (var current = node; current != null; current = current.Parent)
            {
                switch (current)
                {
                    case AnonymousFunctionExpressionSyntax function when IsExpressionTree(model.GetTypeInfo(function).ConvertedType, lambdaExpression):
                        return true;
                    case MemberDeclarationSyntax:
                    case LocalFunctionStatementSyntax:
                        return false;
                }
            }

            return false;
        }

        private static bool IsExpressionTree(ITypeSymbol? type, INamedTypeSymbol lambdaExpression)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, lambdaExpression))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
