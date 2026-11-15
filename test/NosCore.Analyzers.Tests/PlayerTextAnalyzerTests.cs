using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.PlayerTextAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class PlayerTextAnalyzerTests
    {
        private const string Source = @"
namespace NosCore.Packets.ServerPackets.Chats
{{
    public class SayPacket {{ public string Message {{ get; set; }} public string Name {{ get; set; }} }}
}}
namespace NosCore.Packets.ClientPackets.Chats
{{
    public class ClientSayPacket {{ public string Message {{ get; set; }} }}
}}

public class Program
{{
    public object Run(string name) => {0};
}}";

        [DataTestMethod]
        [DataRow("new NosCore.Packets.ServerPackets.Chats.SayPacket { Message = \"Welcome\" }", "\"Welcome\"")]
        [DataRow("new NosCore.Packets.ServerPackets.Chats.SayPacket { Message = $\"Hello {name}\" }", "$\"Hello {name}\"")]
        public async Task WrittenTextIsReported(string expression, string value)
        {
            var column = 39 + expression.IndexOf(value);
            var expected = VerifyCS.Diagnostic().WithSpan(13, column, 13, column + value.Length).WithArguments("SayPacket", "Message");
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), expected);
        }

        [DataTestMethod]
        [DataRow("new NosCore.Packets.ServerPackets.Chats.SayPacket { Message = name }")]
        [DataRow("new NosCore.Packets.ServerPackets.Chats.SayPacket { Message = $\"[{name}]:{name}\" }")]
        [DataRow("new NosCore.Packets.ServerPackets.Chats.SayPacket { Name = \"Buddy\" }")]
        [DataRow("new NosCore.Packets.ClientPackets.Chats.ClientSayPacket { Message = \"Hello\" }")]
        public async Task OtherValuesAreNotReported(string expression)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression));
        }
    }
}
