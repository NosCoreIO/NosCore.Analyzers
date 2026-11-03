using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.DateTimeNowAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class DateTimeNowAnalyzerTests
    {
        private const string Source = @"
using System;

public class Program
{{
    public object Read() => {0};
}}";

        [DataTestMethod]
        [DataRow("DateTime.Now", "DateTime", "Now")]
        [DataRow("DateTime.UtcNow", "DateTime", "UtcNow")]
        [DataRow("DateTime.Today", "DateTime", "Today")]
        [DataRow("DateTimeOffset.Now", "DateTimeOffset", "Now")]
        [DataRow("System.DateTimeOffset.UtcNow", "DateTimeOffset", "UtcNow")]
        public async Task ReadingTheSystemClockIsReported(string expression, string type, string property)
        {
            var expected = VerifyCS.Diagnostic().WithSpan(6, 29, 6, 29 + expression.Length).WithArguments(type, property);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), expected);
        }

        [DataTestMethod]
        [DataRow("DateTime.MinValue")]
        [DataRow("new DateTime(2020, 1, 1).Date")]
        [DataRow("DateTime.Now2")]
        [DataRow("nameof(DateTime.Now)")]
        public async Task OtherDateTimeMembersAreNotReported(string expression)
        {
            var source = string.Format(Source, expression)
                + "\npublic static class Extra { public static DateTime Now2 => default; }";
            source = source.Replace("DateTime.Now2", "Extra.Now2");
            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
