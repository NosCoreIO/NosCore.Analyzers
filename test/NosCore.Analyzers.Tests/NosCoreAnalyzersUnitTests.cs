using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.I18NPacketAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class NosCoreAnalyzersUnitTest
    {
        public readonly string TestClass = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace NosCore.Example
    {{

        [AttributeUsage(AttributeTargets.Field)]
        public class Game18NArgumentsAttribute : Attribute
        {{
            public Game18NArgumentsAttribute(params Type[] argumentTypes)
            {{
                ArgumentTypes = argumentTypes;
            }}

            public Type[] ArgumentTypes {{ get; set; }}
        }}

        public enum Game18NConstString
        {{
            [Game18NArguments(typeof(long), typeof(string))]
            Test
        }}

        public class Packet {{
            public Game18NConstString Message {{ get; set; }}

            public object[] Game18NArguments {{ get; set; }}
        }}

        public class TestObj {{

            public TestObj(int testInt, string testString) {{
                TestInt = testInt;
                TestString = testString;
            }}
            public int TestInt {{ get; set; }}
            public string TestString {{ get; set; }}
        }}

        public class Program
        {{ 
            public void Main() 
            {{
                var testString = ""testString"";
                var testInt = 123;
                var testObj = new TestObj(123, ""testString"");
                var test = new Packet
                {{
                    Message = Game18NConstString.Test,
                    Game18NArguments = new object[] {{ {0} }}
                }};
            }}
        }}
    }}";

        //No diagnostics expected to show up
        [TestMethod]
        public async Task TestWithWrongArgumentCount()
        {
            var test = string.Format(TestClass, "123");

            var result = VerifyCS.Diagnostic().WithSpan(52, 28, 56, 18).WithArguments("long,string");
            await VerifyCS.VerifyAnalyzerAsync(test, result);
        }


        [TestMethod]
        public async Task TestWithRightArgumentCount()
        {
            var test = string.Format(TestClass, "123, \"Gold\"");

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestWithRightArgumentCountButWrongType()
        {
            var test = string.Format(TestClass, "\"test\", \"Gold\"");

            var result = VerifyCS.Diagnostic().WithSpan(52, 28, 56, 18).WithArguments("long,string");
            await VerifyCS.VerifyAnalyzerAsync(test, result);
        }

        [TestMethod]
        public async Task TestWithVariables()
        {
            var test = string.Format(TestClass, "testInt, testString");
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestWithProperties()
        {
            var test = string.Format(TestClass, "testObj.TestInt, testObj.TestString");
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}