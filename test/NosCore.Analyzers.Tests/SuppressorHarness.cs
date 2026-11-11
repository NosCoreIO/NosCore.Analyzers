using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NosCore.Analyzers.Tests
{
    internal static class SuppressorHarness
    {
        private static readonly ImmutableArray<MetadataReference> References = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(path) is "netstandard.dll" or "mscorlib.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        /// <summary>
        /// Compiles the sources with nullable enabled and returns every nullable warning, with IsSuppressed set by the
        /// suppressor under test.
        /// </summary>
        public static async Task<ImmutableArray<Diagnostic>> NullableWarningsAsync(DiagnosticSuppressor suppressor, params string[] sources)
        {
            var compilation = CSharpCompilation.Create(
                "Test",
                sources.Select(source => CSharpSyntaxTree.ParseText(source)),
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            var diagnostics = await compilation
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(suppressor), new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), null, concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true))
                .GetAllDiagnosticsAsync();
            return diagnostics.Where(d => d.Id.StartsWith("CS86", StringComparison.Ordinal)).ToImmutableArray();
        }
    }
}
