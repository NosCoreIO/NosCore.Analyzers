using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.LogLiteralAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class LogLiteralAnalyzerTests
    {
        private const string Source = @"
using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging
{{
    public interface ILogger {{ }}

    public static class LoggerExtensions
    {{
        public static void LogInformation(this ILogger logger, string message, params object[] args) {{ }}
        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args) {{ }}
    }}
}}

public class Program
{{
    public void Run(ILogger logger, Exception exception, string key, int id)
    {{
        {0};
    }}
}}";

        [DataTestMethod]
        [DataRow("logger.LogInformation(\"Arena failed\")", "\"Arena failed\"", "LogInformation")]
        [DataRow("logger.LogError(exception, \"Arena failed\")", "\"Arena failed\"", "LogError")]
        [DataRow("logger.LogInformation($\"{key} [Y/n]\")", "$\"{key} [Y/n]\"", "LogInformation")]
        [DataRow("logger.LogInformation(\"Player \" + id)", "\"Player \" + id", "LogInformation")]
        [DataRow("logger.LogInformation(id > 0 ? \"Player found\" : key)", "id > 0 ? \"Player found\" : key", "LogInformation")]
        public async Task WrittenTextIsReported(string statement, string message, string method)
        {
            var column = 9 + statement.IndexOf(message);
            var expected = VerifyCS.Diagnostic().WithSpan(20, column, 20, column + message.Length).WithArguments(method);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement), expected);
        }

        [DataTestMethod]
        [DataRow("logger.LogInformation(key)")]
        [DataRow("logger.LogInformation(key, id)")]
        [DataRow("logger.LogError(exception, key)")]
        [DataRow("logger.LogInformation($\"{key}: {id}\")")]
        [DataRow("logger.LogInformation(\"\")")]
        [DataRow("logger.LogInformation(id > 0 ? key : key + \":\")")]
        public async Task KeysAndSeparatorsAreNotReported(string statement)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement));
        }
    }
}
