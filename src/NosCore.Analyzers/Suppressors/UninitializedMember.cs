using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Suppressors
{
    internal static class UninitializedMember
    {
        /// <summary>
        /// CS8618 sits on the member itself when the type has no constructor, and on the constructor otherwise, with
        /// the member only named in the message.
        /// </summary>
        public static ISymbol? Find(Diagnostic diagnostic, SuppressionAnalysisContext context)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree == null)
            {
                return null;
            }

            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var model = context.GetSemanticModel(tree);
            return node switch
            {
                VariableDeclaratorSyntax variable => model.GetDeclaredSymbol(variable, context.CancellationToken),
                PropertyDeclarationSyntax property => model.GetDeclaredSymbol(property, context.CancellationToken),
                ConstructorDeclarationSyntax constructor => NamedInMessage(diagnostic, model.GetDeclaredSymbol(constructor, context.CancellationToken)?.ContainingType),
                _ => null,
            };
        }

        private static ISymbol? NamedInMessage(Diagnostic diagnostic, INamedTypeSymbol? type)
        {
            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            var start = message.IndexOf('\'');
            var end = start < 0 ? -1 : message.IndexOf('\'', start + 1);
            if (type == null || end < 0)
            {
                return null;
            }

            var name = message.Substring(start + 1, end - start - 1);
            return type.GetMembers(name).FirstOrDefault(m => m is IFieldSymbol or IPropertySymbol);
        }
    }
}
