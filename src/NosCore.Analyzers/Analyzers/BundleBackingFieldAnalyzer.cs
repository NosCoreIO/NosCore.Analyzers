using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BundleBackingFieldAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreBundleBackingField";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Component bundles must not declare backing fields",
            "'{0}' stores state on bundle '{1}'; put it in a component and expose a computed view instead",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Components own all entity state. Hand-written members of a [ComponentBundle] struct are computed views only, so they cannot declare fields or auto-properties.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
        }

        private static void Analyze(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            if (symbol.IsImplicitlyDeclared || symbol.ContainingType is not { TypeKind: TypeKind.Struct } bundle || !IsBundle(bundle))
            {
                return;
            }

            var storesState = symbol switch
            {
                IFieldSymbol field => !field.IsConst,
                IPropertySymbol property => IsAutoProperty(property),
                _ => false,
            };
            if (!storesState)
            {
                return;
            }

            foreach (var location in symbol.Locations.Where(l => l.IsInSource && !IsGenerated(l)))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, symbol.Name, bundle.Name));
            }
        }

        private static bool IsAutoProperty(IPropertySymbol property) =>
            property.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .OfType<PropertyDeclarationSyntax>()
                .Any(p => p.AccessorList != null
                    && !p.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword) || m.IsKind(SyntaxKind.ExternKeyword))
                    && p.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null));

        private static bool IsBundle(INamedTypeSymbol type) =>
            type.GetAttributes().Any(a => a.AttributeClass?.Name == "ComponentBundleAttribute");

        private static bool IsGenerated(Location location)
        {
            var path = location.SourceTree?.FilePath ?? string.Empty;
            return path.EndsWith(".g.cs") || path.EndsWith(".generated.cs");
        }
    }
}
