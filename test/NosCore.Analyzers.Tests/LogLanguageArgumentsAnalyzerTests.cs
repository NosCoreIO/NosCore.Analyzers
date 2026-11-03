using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NosCore.Analyzers.Analyzers;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.LogLanguageArgumentsAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class LogLanguageArgumentsAnalyzerTests
    {
        private const string Resx = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <data name=""NONE"" xml:space=""preserve""><value>Item not found.</value></data>
  <data name=""TWO"" xml:space=""preserve""><value>Client {0} : {1} authenticated</value></data>
  <data name=""NAMED"" xml:space=""preserve""><value>Session {SessionId} lost</value></data>
</root>";

        private const string FrenchResx = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <data name=""NONE"" xml:space=""preserve""><value>{0} {1} {2}</value></data>
</root>";

        private const string Source = @"
using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging
{{
    public interface ILogger {{ }}

    public static class LoggerExtensions
    {{
        public static void LogError(this ILogger logger, string message, params object[] args) {{ }}
        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args) {{ }}
    }}
}}

public enum LogLanguageKey {{ NONE, TWO, NAMED, MISSING }}

public class LocalizedString
{{
    public static implicit operator string(LocalizedString value) => """";
}}

public interface ILogLanguageLocalizer<T>
{{
    LocalizedString this[T key] {{ get; }}
    LocalizedString this[T key, params object[] arguments] {{ get; }}
}}

public class Program
{{
    public void Run(ILogger logger, ILogLanguageLocalizer<LogLanguageKey> logLanguage, Exception e)
    {{
        {0};
    }}
}}";

        private static async Task VerifyAsync(string statement, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test { TestCode = string.Format(Source, statement) };
            test.TestState.AdditionalFiles.Add(("LocalizedResources.resx", Resx));
            test.TestState.AdditionalFiles.Add(("LocalizedResources.fr.resx", FrenchResx));
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        private static DiagnosticResult Expect(string statement, string templateExpression, string key, int expected, int given)
        {
            var column = 9 + statement.IndexOf(templateExpression, System.StringComparison.Ordinal);
            return VerifyCS.Diagnostic().WithSpan(33, column, 33, column + templateExpression.Length)
                .WithArguments($"LogLanguageKey.{key}", expected, given);
        }

        [DataTestMethod]
        [DataRow("logger.LogError(logLanguage[LogLanguageKey.TWO], 1, 2)")]
        [DataRow("logger.LogError(e, logLanguage[LogLanguageKey.TWO], 1, 2)")]
        [DataRow("logger.LogError(logLanguage[LogLanguageKey.NONE])")]
        [DataRow("logger.LogError(logLanguage[LogLanguageKey.NAMED], 7)")]
        [DataRow("logger.LogError(string.Format(logLanguage[LogLanguageKey.TWO], 1, 2))")]
        [DataRow("logger.LogError(logLanguage[LogLanguageKey.TWO, 1, 2])")]
        [DataRow("logger.LogError(logLanguage[LogLanguageKey.MISSING], 1)")]
        public async Task MatchingOrUnknownCountsAreNotReported(string statement)
        {
            await VerifyAsync(statement);
        }

        [TestMethod]
        public async Task AnExplicitArrayIsCounted()
        {
            const string statement = "logger.LogError(logLanguage[LogLanguageKey.TWO], new object[] { 1 })";
            await VerifyAsync(statement, Expect(statement, "logLanguage[LogLanguageKey.TWO]", "TWO", 2, 1));
        }

        [TestMethod]
        public async Task ExtraArgumentsToTheLoggerAreReported()
        {
            const string statement = "logger.LogError(logLanguage[LogLanguageKey.NONE], 1, 2)";
            await VerifyAsync(statement, Expect(statement, "logLanguage[LogLanguageKey.NONE]", "NONE", 0, 2));
        }

        [TestMethod]
        public async Task MissingArgumentsAfterAnExceptionAreReported()
        {
            const string statement = "logger.LogError(e, logLanguage[LogLanguageKey.TWO], 1)";
            await VerifyAsync(statement, Expect(statement, "logLanguage[LogLanguageKey.TWO]", "TWO", 2, 1));
        }

        [TestMethod]
        public async Task StringFormatArgumentsAreReported()
        {
            const string statement = "logger.LogError(string.Format(logLanguage[LogLanguageKey.NONE], 1, 2))";
            await VerifyAsync(statement, Expect(statement, "logLanguage[LogLanguageKey.NONE]", "NONE", 0, 2));
        }

        [TestMethod]
        public async Task IndexerArgumentsAreReported()
        {
            const string statement = "logger.LogError(logLanguage[LogLanguageKey.TWO, 1])";
            await VerifyAsync(statement, Expect(statement, "logLanguage[LogLanguageKey.TWO, 1]", "TWO", 2, 1));
        }

        [DataTestMethod]
        [DataRow("no placeholder", 0)]
        [DataRow("{0} and {1}", 2)]
        [DataRow("{1} only", 2)]
        [DataRow("{0:N2} {0,5}", 1)]
        [DataRow("{{0}} escaped", 0)]
        [DataRow("{Name} and {Other} and {Name}", 2)]
        public void PlaceholdersAreCounted(string text, int expected)
        {
            Assert.AreEqual(expected, LogLanguageArgumentsAnalyzer.CountPlaceholders(text));
        }
    }
}
