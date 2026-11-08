using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.PacketLiteralMessageAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class PacketLiteralMessageAnalyzerTests
    {
        private const string Source = @"
namespace NosCore.Packets
{{
    public abstract class PacketBase {{ }}

    public class MsgPacket : PacketBase {{ public string Message {{ get; set; }} }}
    public class MsgiPacket : PacketBase {{ public int Message {{ get; set; }} }}
    public class GuriPacket : PacketBase {{ public string Message {{ get; set; }} }}
}}

public class Program
{{
    public object Build(string text)
    {{
        return new NosCore.Packets.{0} {{ Message = {1} }};
    }}
}}";

        [TestMethod]
        public async Task LiteralInPacketWithI18NTwinIsReported()
        {
            var expected = VerifyCS.Diagnostic().WithSpan(15, 58, 15, 65).WithArguments("MsgPacket", "Message", "MsgiPacket");
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, "MsgPacket", "\"Hello\""), expected);
        }

        [DataTestMethod]
        [DataRow("MsgPacket", "text")]
        [DataRow("MsgPacket", "\"\"")]
        [DataRow("MsgPacket", "\" \"")]
        [DataRow("GuriPacket", "\"Hello\"")]
        public async Task OtherAssignmentsAreNotReported(string type, string value)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, type, value));
        }
    }
}
