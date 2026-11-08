using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.ConfigureAwaitAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class ConfigureAwaitAnalyzerTests
    {
        private const string Source = @"
using System.Threading.Tasks;

public class Program
{{
    public async Task Run(Task task, Task<int> value, ValueTask valueTask)
    {{
        {0};
    }}

    private static object ConfigureAwait(bool value) => value;
}}";

        [DataTestMethod]
        [DataRow("await task.ConfigureAwait(false)", "task.ConfigureAwait(false)")]
        [DataRow("await value.ConfigureAwait(true)", "value.ConfigureAwait(true)")]
        [DataRow("await valueTask.ConfigureAwait(false)", "valueTask.ConfigureAwait(false)")]
        public async Task ConfigureAwaitIsReported(string statement, string invocation)
        {
            var column = 9 + statement.IndexOf(invocation);
            var expected = VerifyCS.Diagnostic().WithSpan(8, column, 8, column + invocation.Length);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement), expected);
        }

        [DataTestMethod]
        [DataRow("await task")]
        [DataRow("ConfigureAwait(false)")]
        public async Task OtherCallsAreNotReported(string statement)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement));
        }
    }
}
