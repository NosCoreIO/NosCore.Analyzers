using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PacketTextAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCorePacketText";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Do not write packets as text",
            "This string is a '{0}' packet written as text; build the typed packet and compare it with PacketAssert",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Packets are handled as objects, in tests too. A line the client cannot read is fixed in the packet class, not matched as text.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var attribute = start.Compilation.GetTypeByMetadataName("NosCore.Packets.Attributes.PacketHeaderAttribute");
                if (attribute == null)
                {
                    return;
                }

                var headers = CollectHeaders(attribute);
                if (headers.Count == 0)
                {
                    return;
                }

                start.RegisterSyntaxNodeAction(node => AnalyzeLiteral(node, headers), SyntaxKind.StringLiteralExpression);
                start.RegisterSyntaxNodeAction(node => AnalyzeInterpolation(node, headers), SyntaxKind.InterpolatedStringExpression);
            });
        }

        private static HashSet<string> CollectHeaders(INamedTypeSymbol attribute)
        {
            var headers = new HashSet<string>();
            var pending = new Stack<INamespaceOrTypeSymbol>();
            pending.Push(attribute.ContainingAssembly.GlobalNamespace);
            while (pending.Count > 0)
            {
                foreach (var member in pending.Pop().GetMembers())
                {
                    if (member is INamespaceOrTypeSymbol container)
                    {
                        pending.Push(container);
                    }

                    if (member is not INamedTypeSymbol type)
                    {
                        continue;
                    }

                    foreach (var data in type.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                            && data.ConstructorArguments.Length > 0
                            && data.ConstructorArguments[0].Value is string header
                            && header.Any(char.IsLetter))
                        {
                            headers.Add(header);
                        }
                    }
                }
            }

            return headers;
        }

        private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context, HashSet<string> headers)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            if (literal.Parent is InterpolatedStringContentSyntax)
            {
                return;
            }

            Report(context, literal.Token.ValueText, headers);
        }

        private static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context, HashSet<string> headers)
        {
            var text = new StringBuilder();
            foreach (var content in ((InterpolatedStringExpressionSyntax)context.Node).Contents)
            {
                text.Append(content is InterpolatedStringTextSyntax part ? part.TextToken.ValueText : "0");
            }

            Report(context, text.ToString(), headers);
        }

        private static void Report(SyntaxNodeAnalysisContext context, string text, HashSet<string> headers)
        {
            var header = PacketHeaderOf(text, headers);
            if (header != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), header));
            }
        }

        // A packet line is its header followed by fields that are numbers or short tokens, most of them numeric.
        internal static string? PacketHeaderOf(string text, ICollection<string> headers)
        {
            var tokens = text.Split(' ');
            if (tokens.Length < 3 || !headers.Contains(tokens[0]))
            {
                return null;
            }

            var fields = tokens.Skip(1).ToList();
            if (fields.Any(field => field.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '.' or '|' or '^' or '_' or '#' or ':' or '/'))))
            {
                return null;
            }

            return fields.Count(field => field.Any(char.IsDigit)) * 2 >= fields.Count ? tokens[0] : null;
        }
    }
}
