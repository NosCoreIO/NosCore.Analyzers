using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NosCore.Analyzers.Analyzers
{
    internal sealed class EfQueryLambda
    {
        private readonly INamedTypeSymbol? _lambdaExpression;
        private readonly INamedTypeSymbol? _queryable;
        private readonly INamedTypeSymbol? _dao;

        public EfQueryLambda(Compilation compilation)
        {
            _lambdaExpression = compilation.GetTypeByMetadataName("System.Linq.Expressions.LambdaExpression");
            _queryable = compilation.GetTypeByMetadataName("System.Linq.Queryable");
            _dao = compilation.GetTypeByMetadataName("NosCore.Dao.Interfaces.IDao`2");
        }

        public bool IsInside(SyntaxNode node, SemanticModel model)
        {
            if (_lambdaExpression == null)
            {
                return false;
            }

            for (var current = node; current != null; current = current.Parent)
            {
                switch (current)
                {
                    case AnonymousFunctionExpressionSyntax function
                        when IsExpressionTree(model.GetTypeInfo(function).ConvertedType) && IsPassedToEf(function, model):
                        return true;
                    case MemberDeclarationSyntax:
                    case LocalFunctionStatementSyntax:
                        return false;
                }
            }

            return false;
        }

        private bool IsExpressionTree(ITypeSymbol? type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, _lambdaExpression))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPassedToEf(AnonymousFunctionExpressionSyntax function, SemanticModel model)
        {
            if (function.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } }
                || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            {
                return false;
            }

            var type = (method.ReducedFrom ?? method).ContainingType.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(type, _queryable) || IsInEfCore(type.ContainingNamespace))
            {
                return true;
            }

            return _dao != null && (SymbolEqualityComparer.Default.Equals(type, _dao)
                || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _dao)));
        }

        private static bool IsInEfCore(INamespaceSymbol? ns)
        {
            for (; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
            {
                if (ns.Name == "EntityFrameworkCore" && ns.ContainingNamespace is { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
