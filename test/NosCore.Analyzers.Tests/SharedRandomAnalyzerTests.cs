using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.SharedRandomAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class SharedRandomAnalyzerTests
    {
        private const string Source = @"
using System;

public class Program
{{
    public object Roll() => {0};
}}
namespace NosCore.Shared.Helpers {{ public class RandomHelper {{ public static RandomHelper Instance => null; }} }}";

        [DataTestMethod]
        [DataRow("Random.Shared", "Random.Shared")]
        [DataRow("new Random()", "new Random")]
        [DataRow("new Random(42)", "new Random")]
        [DataRow("NosCore.Shared.Helpers.RandomHelper.Instance", "RandomHelper.Instance")]
        public async Task RollingOnASharedRandomIsReported(string expression, string name)
        {
            var expected = VerifyCS.Diagnostic().WithSpan(6, 29, 6, 29 + expression.Length).WithArguments(name);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), expected);
        }

        [DataTestMethod]
        [DataRow("nameof(Random.Shared)")]
        [DataRow("typeof(Random)")]
        [DataRow("new object()")]
        public async Task OtherExpressionsAreNotReported(string expression)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression));
        }

        [TestMethod]
        public async Task AnInjectedRandomIsNotReported()
        {
            const string source = @"
using System;

public class Program
{
    private readonly Random _random;
    public Program(Random random) => _random = random;
    public int Roll() => _random.Next(0, 100);
}";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
