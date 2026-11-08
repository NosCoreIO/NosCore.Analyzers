using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.BundleBackingFieldAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class BundleBackingFieldAnalyzerTests
    {
        private const string Source = @"
using System;

public class ComponentBundleAttribute : Attribute {{ }}

[ComponentBundle]
public partial struct PlayerBundle
{{
    {0}
}}

public partial struct PlayerBundle
{{
    public short PositionX => 1;
}}

public struct NotABundle
{{
    private int _count;
    public int Level {{ get; set; }}
}}";

        [DataTestMethod]
        [DataRow("private int _count;", "_count")]
        [DataRow("private static readonly object Cache = new();", "Cache")]
        [DataRow("public int Level { get; set; }", "Level")]
        [DataRow("public int Level { get; }", "Level")]
        public async Task StateOnBundleIsReported(string member, string name)
        {
            var column = 5 + member.IndexOf(name);
            var expected = VerifyCS.Diagnostic().WithSpan(9, column, 9, column + name.Length).WithArguments(name, "PlayerBundle");
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, member), expected);
        }

        [DataTestMethod]
        [DataRow("public short MapX => PositionX;")]
        [DataRow("private const int Max = 3;")]
        [DataRow("public int Twice() => PositionX * 2;")]
        public async Task ComputedMembersAreNotReported(string member)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, member));
        }
    }
}
