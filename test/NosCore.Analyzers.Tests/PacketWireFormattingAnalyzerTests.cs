using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.PacketWireFormattingAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class PacketWireFormattingAnalyzerTests
    {
        private const string Source = @"
using NosCore.Packets;

namespace NosCore.Packets
{{
    public abstract class PacketBase {{ }}
}}

public class MlinfoPacket : PacketBase
{{
    public string Message {{ get; set; }}
}}

public class Holder
{{
    public string Message {{ get; set; }}
}}

public class Program
{{
    public object Build(string text, int a, int b, int[] values)
    {{
        var target = new {0} {{ Message = {1} }};
        return target;
    }}
}}";

        private const int Line = 23;

        private static int Column(string packet) => 26 + packet.Length + " { Message = ".Length;

        [DataTestMethod]
        [DataRow("text.Replace(' ', '^')")]
        [DataRow("text.Replace(\" \", \"^\")")]
        [DataRow("string.Join(\"|\", values)")]
        [DataRow("string.Join('.', values)")]
        [DataRow("$\"{a}.{b}\"")]
        [DataRow("$\"{a}|{b}|{text}\"")]
        [DataRow("$\"{text}^\"")]
        [DataRow("a + \".\" + b")]
        public async Task HandBuiltWireFormatOnPacketIsReported(string value)
        {
            var column = Column("MlinfoPacket");
            var expected = VerifyCS.Diagnostic().WithSpan(Line, column, Line, column + value.Length).WithArguments("Message", Reason(value));
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, "MlinfoPacket", value), expected);
        }

        [DataTestMethod]
        [DataRow("MlinfoPacket", "text")]
        [DataRow("MlinfoPacket", "text.Replace('a', 'b')")]
        [DataRow("MlinfoPacket", "string.Join(\", \", values)")]
        [DataRow("MlinfoPacket", "$\"{a} points earned.\"")]
        [DataRow("MlinfoPacket", "\"a.b\"")]
        [DataRow("Holder", "text.Replace(' ', '^')")]
        [DataRow("Holder", "string.Join(\"|\", values)")]
        public async Task OtherAssignmentsAreNotReported(string type, string value)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, type, value));
        }

        private static string Reason(string value) =>
            value.Contains("Replace") ? "a separator replaced by hand"
            : value.Contains("Join") ? "values joined with a packet separator"
            : value.StartsWith("$") ? "an interpolated string with a packet separator"
            : "a concatenation with a packet separator";
    }
}
