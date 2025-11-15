using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class I18NPacketAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreAnalyzers";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.Resources.AnalyzerTitle), Resources.Resources.ResourceManager, typeof(Resources.Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.Resources.AnalyzerMessageFormat), Resources.Resources.ResourceManager, typeof(Resources.Resources));
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyseInvocation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyseInvocation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not ObjectCreationExpressionSyntax node)
            {
                return;
            }

            var type = context.SemanticModel.GetTypeInfo(node).Type;
            var properties = type?.GetMembers().Where(m => m.Kind == SymbolKind.Property).OfType<IPropertySymbol>().ToList();

            var enumProperty = properties?.FirstOrDefault(x => x.Type.Name == "Game18NConstString");
            if (enumProperty == null)
            {
                return;
            }

            var argumentProperty = properties?.FirstOrDefault(x => x.Type.TypeKind == TypeKind.Array && (x.Type as IArrayTypeSymbol)?.ElementType.Name == nameof(Object));
            if (argumentProperty == null)
            {
                return;
            }


            var assignments = node.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().ToList();
            var enumAssignment = assignments?.FirstOrDefault(x => (x?.Left as IdentifierNameSyntax)?.Identifier.Value?.ToString() == enumProperty.Name);
            var argumentAssignment = assignments?.FirstOrDefault(x => (x?.Left as IdentifierNameSyntax)?.Identifier.Value?.ToString() == argumentProperty.Name);

            var arguments = new List<string>();
            if (enumAssignment != null)
            {
                var enumValue = (enumAssignment.Right as MemberAccessExpressionSyntax);
                var enumType = enumValue != null ? context.SemanticModel.GetTypeInfo(enumValue).Type : null;
                var fields = enumType?.GetMembers().Where(m => m.Kind == SymbolKind.Field).OfType<IFieldSymbol>().ToList();
                var field = fields?.FirstOrDefault(x => x.Name == enumValue?.Name.ToString()) ?? fields?.First() ?? throw new ArgumentException();
                var attribute = field.GetAttributes().FirstOrDefault(x => x.AttributeClass?.Name == "Game18NArgumentsAttribute");
                arguments.AddRange(attribute?.ConstructorArguments.First().Values.Where(x=>x.Value != null).Select(x => x.Value!.ToString()).Where(x => x != null).Cast<string>() ?? new List<string>());
            }

            var error = false;
            if (argumentAssignment != null)
            {
                var expressions = (argumentAssignment.Right as ArrayCreationExpressionSyntax)?.Initializer?.Expressions.ToList() ?? new List<ExpressionSyntax>();

                if (arguments.Count != expressions.Count)
                {
                    error = true;
                }
                else
                {
                    for (var index = 0; index < arguments.Count; index++)
                    {
                        var expression = expressions[index];
                        var argument = arguments[index];
                        var literal = expression as LiteralExpressionSyntax;
                        var variable = context.SemanticModel.GetTypeInfo(expression).Type;
                        bool? isValid;
                        switch (argument)
                        {
                            case "string":
                                isValid = literal?.IsKind(SyntaxKind.StringLiteralExpression)
                                          ?? variable?.Name == nameof(String);
                                if (isValid != true)
                                {
                                    error = true;
                                }

                                break;

                            case "long":
                                isValid = literal?.IsKind(SyntaxKind.NumericLiteralExpression)
                                          ?? variable?.Name is nameof(Int32) or nameof(Int16) or nameof(Int64);
                                if (isValid != true)
                                {
                                    error = true;
                                }

                                break;

                            default:
                                error = true;
                                break;
                        }
                    }
                }
            }
            if (error)
            {
                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), string.Join(",", arguments));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
