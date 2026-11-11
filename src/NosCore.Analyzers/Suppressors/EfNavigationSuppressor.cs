using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Suppressors
{
    /// <summary>
    /// A required reference navigation on an Entity Framework entity is filled by EF when it loads the entity, so it
    /// has nothing to be initialized to in code.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EfNavigationSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor Descriptor = new(
            "NosCoreEfNavigation8618",
            "CS8618",
            "The member is an Entity Framework reference navigation, which EF sets when it loads the entity.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Descriptor);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var dbContext = context.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
            var dbSet = context.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
            if (dbContext == null || dbSet == null)
            {
                return;
            }

            HashSet<ITypeSymbol>? entities = null;
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                if (UninitializedMember.Find(diagnostic, context) is not IPropertySymbol property)
                {
                    continue;
                }

                entities ??= FindEntities(context.Compilation.Assembly.GlobalNamespace, dbContext, dbSet);
                if (entities.Contains(property.ContainingType) && entities.Contains(property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)))
                {
                    context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
                }
            }
        }

        /// <summary>
        /// Every DbSet type, plus every source class reachable from one through its properties, since EF maps an
        /// entity it discovers through a navigation even without a DbSet of its own.
        /// </summary>
        private static HashSet<ITypeSymbol> FindEntities(INamespaceSymbol root, INamedTypeSymbol dbContext, INamedTypeSymbol dbSet)
        {
            var entities = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var pending = new Stack<INamedTypeSymbol>();
            foreach (var context in AllTypes(root).Where(t => InheritsFrom(t, dbContext)))
            {
                foreach (var set in context.GetMembers().OfType<IPropertySymbol>().Select(p => p.Type).OfType<INamedTypeSymbol>()
                    .Where(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, dbSet)))
                {
                    if (set.TypeArguments[0] is INamedTypeSymbol entity)
                    {
                        pending.Push(entity);
                    }
                }
            }

            while (pending.Count > 0)
            {
                var entity = pending.Pop();
                if (!entities.Add(entity.WithNullableAnnotation(NullableAnnotation.NotAnnotated)))
                {
                    continue;
                }

                foreach (var property in entity.GetMembers().OfType<IPropertySymbol>())
                {
                    var type = property.Type as INamedTypeSymbol;
                    if (type is { IsGenericType: true } && type.TypeArguments.Length == 1)
                    {
                        type = type.TypeArguments[0] as INamedTypeSymbol;
                    }

                    if (type is { TypeKind: TypeKind.Class } && type.Locations.Any(l => l.IsInSource))
                    {
                        pending.Push(type);
                    }
                }
            }

            return entities;
        }

        private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol root)
        {
            var pending = new Stack<INamespaceOrTypeSymbol>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current is INamespaceSymbol ns)
                {
                    foreach (var child in ns.GetMembers())
                    {
                        pending.Push(child);
                    }
                }
                else if (current is INamedTypeSymbol type)
                {
                    yield return type;
                }
            }
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
