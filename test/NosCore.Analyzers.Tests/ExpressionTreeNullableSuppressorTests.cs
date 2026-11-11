using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.ExpressionTreeNullableSuppressor>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class ExpressionTreeNullableSuppressorTests
    {
        private const string Source = @"#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

public class Item {{ public string Name = """"; }}
public class Row {{ public Item? Item; public int? VNum; }}

public class Program
{{
    private static string NameOf(Item item) => item.Name;
    private static void Run(Expression<Func<Row, object>> selector) {{ }}

    public void Query(IQueryable<Row> rows, IEnumerable<Row> list)
    {{
        {0};
    }}
}}";

        private static Task VerifyAsync(string statement, string id, string operand, bool suppressed, params string[] arguments)
        {
            var expected = Array.Empty<DiagnosticResult>();
            for (var index = statement.IndexOf(operand, StringComparison.Ordinal); index >= 0; index = statement.IndexOf(operand, index + 1, StringComparison.Ordinal))
            {
                var column = 9 + index;
                expected = [.. expected, DiagnosticResult.CompilerError(id)
                    .WithSpan(17, column, 17, column + operand.Length)
                    .WithArguments(arguments)
                    .WithIsSuppressed(suppressed)];
            }

            return VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement), expected);
        }

        [DataTestMethod]
        [DataRow("rows.Where(r => r.Item.Name == \"a\")")]
        [DataRow("rows.Select(r => r.Item.Name.Length)")]
        [DataRow("rows.OrderBy(r => r.Item.Name).ThenBy(r => r.Item.Name.Length)")]
        [DataRow("rows.Where(o => list.Any(r => r.Item.Name == o.VNum.ToString()))")]
        [DataRow("Run(r => r.Item.Name)")]
        public async Task DereferenceInsideAnExpressionTreeIsSuppressed(string statement)
        {
            await VerifyAsync(statement, "CS8602", "r.Item", true);
        }

        [TestMethod]
        public async Task NullableValueAccessInsideAnExpressionTreeIsSuppressed()
        {
            await VerifyAsync("rows.GroupBy(r => r.VNum.Value)", "CS8629", "r.VNum", true);
        }

        [TestMethod]
        public async Task PossibleNullArgumentInsideAnExpressionTreeIsSuppressed()
        {
            await VerifyAsync("rows.Select(r => NameOf(r.Item))", "CS8604", "r.Item", true, "item", "string Program.NameOf(Item item)");
        }

        [DataTestMethod]
        [DataRow("list.Where(r => r.Item.Name == \"a\")")]
        [DataRow("rows.AsEnumerable().Select(r => r.Item.Name.Length)")]
        public async Task DereferenceInADelegateIsStillReported(string statement)
        {
            await VerifyAsync(statement, "CS8602", "r.Item", false);
        }

        [TestMethod]
        public async Task PossibleNullArgumentInADelegateIsStillReported()
        {
            await VerifyAsync("list.Select(r => NameOf(r.Item))", "CS8604", "r.Item", false, "item", "string Program.NameOf(Item item)");
        }
    }
}
