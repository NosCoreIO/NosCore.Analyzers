using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.NullForgivingAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class NullForgivingAnalyzerTests
    {
        [TestMethod]
        public async Task NullForgivingOperatorIsReported()
        {
            const string source = @"
#nullable enable
public class Program
{
    private string _name = null{|#0:!|};

    public int Length(string? value) => value{|#1:!|}.Length;
}";
            await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic().WithLocation(0), VerifyCS.Diagnostic().WithLocation(1));
        }

        [TestMethod]
        public async Task LogicalNotIsNotReported()
        {
            const string source = @"
public class Program
{
    public bool Invert(bool value) => !value && value != true;
}";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
