using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    /// <summary>
    /// Checks that a log message read from an ILogLanguageLocalizer gets as many arguments as its text has
    /// placeholders. The texts come from the neutral .resx files passed to the compiler as AdditionalFiles.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LogLanguageArgumentsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NosCoreLogArguments";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Log message argument count does not match its text",
            "'{0}' has {1} placeholder(s) but is given {2} argument(s)",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A log message with fewer arguments than placeholders throws a FormatException when it is written; extra arguments are silently dropped.");

        private const string LocalizerInterface = "ILogLanguageLocalizer";
        private const string LoggerExtensions = "Microsoft.Extensions.Logging.LoggerExtensions";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var placeholders = ReadPlaceholders(start.Options.AdditionalFiles, start.CancellationToken);
                if (placeholders.Count == 0)
                {
                    return;
                }

                start.RegisterOperationAction(operation => Analyze(operation, placeholders), OperationKind.PropertyReference);
            });
        }

        private static void Analyze(OperationAnalysisContext context, IReadOnlyDictionary<string, int> placeholders)
        {
            var reference = (IPropertyReferenceOperation)context.Operation;
            if (!reference.Property.IsIndexer || !IsLogLanguageLocalizer(reference.Property.ContainingType)
                || reference.Arguments.Length == 0
                || reference.Arguments[0].Value is not IFieldReferenceOperation key
                || key.Field.ContainingType.TypeKind != TypeKind.Enum
                || !placeholders.TryGetValue(key.Field.Name, out var expected))
            {
                return;
            }

            var given = reference.Arguments.Length > 1
                ? CountArguments(reference.Arguments.Skip(1))
                : CountConsumerArguments(reference);
            if (given is { } count && count != expected)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, reference.Syntax.GetLocation(),
                    $"{key.Field.ContainingType.Name}.{key.Field.Name}", expected, count));
            }
        }

        private static bool IsLogLanguageLocalizer(INamedTypeSymbol type) =>
            type.Name == LocalizerInterface || type.AllInterfaces.Any(i => i.Name == LocalizerInterface);

        private static int? CountConsumerArguments(IOperation template)
        {
            var current = template.Parent;
            while (current is IConversionOperation)
            {
                current = current.Parent;
            }

            if (current is not IArgumentOperation { Parameter: { } parameter } argument
                || argument.Parent is not IInvocationOperation invocation)
            {
                return null;
            }

            var method = invocation.TargetMethod;
            var isFormat = method.ContainingType.SpecialType == SpecialType.System_String && method.Name == "Format" && parameter.Name == "format";
            var isLog = method.ContainingType.ToDisplayString() == LoggerExtensions && parameter.Name == "message";
            if (!isFormat && !isLog)
            {
                return null;
            }

            return CountArguments(invocation.Arguments.Where(a => a.Parameter != null && a.Parameter.Ordinal > parameter.Ordinal));
        }

        private static int? CountArguments(IEnumerable<IArgumentOperation> arguments)
        {
            var count = 0;
            foreach (var argument in arguments)
            {
                if (argument.ArgumentKind == ArgumentKind.Explicit || argument.ArgumentKind == ArgumentKind.DefaultValue)
                {
                    count += argument.ArgumentKind == ArgumentKind.Explicit ? 1 : 0;
                    continue;
                }

                switch (argument.Value)
                {
                    case IArrayCreationOperation { Initializer: { } initializer }:
                        count += initializer.ElementValues.Length;
                        break;
                    case IArrayCreationOperation creation when creation.DimensionSizes.Length == 1
                        && creation.DimensionSizes[0].ConstantValue is { HasValue: true, Value: int size }:
                        count += size;
                        break;
                    case ICollectionExpressionOperation collection:
                        count += collection.Elements.Length;
                        break;
                    default:
                        return null;
                }
            }

            return count;
        }

        private static IReadOnlyDictionary<string, int> ReadPlaceholders(ImmutableArray<AdditionalText> files, System.Threading.CancellationToken token)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in files.Where(f => IsNeutralResx(f.Path)))
            {
                var text = file.GetText(token)?.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                XDocument document;
                try
                {
                    document = XDocument.Parse(text);
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }

                foreach (var data in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
                {
                    var name = data.Attribute("name")?.Value;
                    var value = data.Element("value")?.Value;
                    if (name != null && value != null && data.Attribute("type") == null)
                    {
                        result[name] = CountPlaceholders(value);
                    }
                }
            }

            return result;
        }

        private static bool IsNeutralResx(string path)
        {
            if (!path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var culture = Path.GetExtension(Path.GetFileNameWithoutExtension(path)).TrimStart('.');
            var language = culture.Split('-')[0];
            return !(language.Length is 2 or 3 && language.All(char.IsLetter) && culture.Length <= 10);
        }

        public static int CountPlaceholders(string text)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++;
                    continue;
                }

                var end = text.IndexOf('}', i + 1);
                if (end < 0)
                {
                    break;
                }

                var hole = text.Substring(i + 1, end - i - 1);
                var stop = hole.IndexOfAny(new[] { ',', ':' });
                names.Add((stop < 0 ? hole : hole.Substring(0, stop)).Trim());
                i = end;
            }

            return names.Count > 0 && names.All(n => int.TryParse(n, out _))
                ? names.Max(int.Parse) + 1
                : names.Count;
        }
    }
}
