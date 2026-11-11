using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.ExpressionTreeNullForgivingAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class ExpressionTreeNullForgivingAnalyzerTests
    {
        private const string Source = @"#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

public class Item {{ public string Name = """"; }}
public class Row {{ public Item? Item; public int? VNum; public int[]? Codes; }}

public class Program
{{
    public object Query(IQueryable<Row> rows, IEnumerable<Row> list) => {0};
}}";

        [DataTestMethod]
        [DataRow("rows.Where(r => r.Item!.Name == \"a\")", "r.Item!")]
        [DataRow("rows.GroupBy(r => r.VNum!.Value)", "r.VNum!")]
        [DataRow("rows.Select(r => r.Codes![0])", "r.Codes!")]
        [DataRow("rows.Where(r => list.Any(l => l.Item!.Name == \"a\"))", "l.Item!")]
        public async Task ForgivingAReceiverInsideAnExpressionTreeIsReported(string expression, string operand)
        {
            var column = 73 + expression.IndexOf(operand, System.StringComparison.Ordinal) + operand.Length - 1;
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression), VerifyCS.Diagnostic().WithSpan(11, column, 11, column + 1));
        }

        [DataTestMethod]
        [DataRow("list.Where(r => r.Item!.Name == \"a\")")]
        [DataRow("rows.Select(r => r.Item!)")]
        [DataRow("rows.ToList().Select(r => r.Item!.Name)")]
        public async Task OtherForgivingOperatorsAreNotReported(string expression)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, expression));
        }
    }
}
