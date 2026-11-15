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
    public async Task Run(Task task, ValueTask valueTask, Task<int> result)
    {{
        {0};
    }}
}}";

        [DataTestMethod]
        [DataRow("await task.ConfigureAwait(false)", 15)]
        [DataRow("await valueTask.ConfigureAwait(false)", 15)]
        [DataRow("await result.ConfigureAwait(true)", 15)]
        public async Task ConfigureAwaitIsReported(string statement, int column)
        {
            var expected = VerifyCS.Diagnostic().WithSpan(8, column, 8, 9 + statement.Length);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement), expected);
        }

        [TestMethod]
        public async Task AwaitingDirectlyIsNotReported()
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, "await task"));
        }

        [TestMethod]
        public async Task AnUnrelatedConfigureAwaitIsNotReported()
        {
            const string source = @"
public class Builder
{
    public Builder ConfigureAwait(bool value) => this;
    public Builder Build() => new Builder().ConfigureAwait(false);
}";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
