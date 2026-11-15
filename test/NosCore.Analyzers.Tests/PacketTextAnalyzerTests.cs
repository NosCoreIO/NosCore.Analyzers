using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.PacketTextAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class PacketTextAnalyzerTests
    {
        private const string Source = @"
namespace NosCore.Packets.Attributes
{{
    public class PacketHeaderAttribute : System.Attribute {{ public PacketHeaderAttribute(string identification) {{ }} }}
}}
namespace NosCore.Packets.ServerPackets
{{
    [NosCore.Packets.Attributes.PacketHeader(""say"")] public class SayPacket {{ }}
    [NosCore.Packets.Attributes.PacketHeader(""n_inv"")] public class NInvPacket {{ }}
    [NosCore.Packets.Attributes.PacketHeader(""0"")] public class ZeroPacket {{ }}
}}

public class Program
{{
    public string Run(int id) => {0};
}}";

        [DataTestMethod]
        [DataRow("\"say 1 2 0 hello\"", "say")]
        [DataRow("\"n_inv 2 2089 0 9679|2|0\"", "n_inv")]
        [DataRow("$\"say 1 {id} 0 hello\"", "say")]
        public async Task PacketLinesAreReported(string expression, string header)
        {
            var expected = VerifyCS.Diagnostic().WithSpan(15, 34, 15, 34 + expression.Length).WithArguments(header);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), expected);
        }

        [DataTestMethod]
        [DataRow("\"say hello to everyone\"")]
        [DataRow("\"say 1\"")]
        [DataRow("\"0 1 2 3 5 9\"")]
        [DataRow("\"say carries the player's text, and -1 without one\"")]
        [DataRow("\"walk 1 2 3\"")]
        public async Task OtherStringsAreNotReported(string expression)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression));
        }
    }
}
