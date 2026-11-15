using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.WireEscapeAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class WireEscapeAnalyzerTests
    {
        private const string Source = @"
public class Program
{{
    public string Run(string text) => {0};
}}";

        [DataTestMethod]
        [DataRow("text.Replace(' ', '^')", "^")]
        [DataRow("text.Replace(\" \", \"^\")", "^")]
        [DataRow("text.Replace(' ', '|')", "|")]
        [DataRow("text.Replace(newChar: '^', oldChar: ' ')", "^")]
        [DataRow("text.Replace((char)32, (char)94)", "^")]
        public async Task EscapingSpacesForTheWireIsReported(string expression, string escape)
        {
            var expected = VerifyCS.Diagnostic().WithSpan(4, 39, 4, 39 + expression.Length).WithArguments(escape);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), expected);
        }

        [DataTestMethod]
        [DataRow("text.Replace(' ', '_')")]
        [DataRow("text.Replace('^', ' ')")]
        [DataRow("text.Replace(\"a\", \"^\")")]
        [DataRow("text.Replace(\" \", text)")]
        [DataRow("text.Replace(newChar: ' ', oldChar: '^')")]
        public async Task OtherReplacementsAreNotReported(string expression)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression));
        }
    }
}
