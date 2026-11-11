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
using Microsoft.EntityFrameworkCore;

public class Item {{ public string Name = """"; }}
public class Row {{ public Item? Item; public int? VNum; }}

public class Program
{{
    private static string NameOf(Item item) => item.Name;
    private static void Run(Expression<Func<Row, object>> selector) {{ }}

    public void Query(IQueryable<Row> rows, IEnumerable<Row> list, NosCore.Dao.Interfaces.IDao<Row, int> dao)
    {{
        {0};
    }}
}}

namespace NosCore.Dao.Interfaces
{{
    public interface IDao<TDto, in TPk> {{ IEnumerable<TDto>? Where(Expression<Func<TDto, bool>> predicate); }}
}}

namespace Microsoft.EntityFrameworkCore
{{
    public static class EntityFrameworkQueryableExtensions
    {{
        public static IQueryable<T> Include<T, TProperty>(this IQueryable<T> source, Expression<Func<T, TProperty>> path) => source;
    }}
}}";

        private static Task VerifyAsync(string statement, string id, string operand, bool suppressed, params string[] arguments)
        {
            var expected = Array.Empty<DiagnosticResult>();
            for (var index = statement.IndexOf(operand, StringComparison.Ordinal); index >= 0; index = statement.IndexOf(operand, index + 1, StringComparison.Ordinal))
            {
                var column = 9 + index;
                expected = [.. expected, DiagnosticResult.CompilerError(id)
                    .WithSpan(18, column, 18, column + operand.Length)
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
        [DataRow("rows.Include(r => r.Item.Name)")]
        [DataRow("dao.Where(r => r.Item.Name == \"a\")")]
        public async Task DereferenceInsideAnEfQueryIsSuppressed(string statement)
        {
            await VerifyAsync(statement, "CS8602", "r.Item", true);
        }

        [TestMethod]
        public async Task NullableValueAccessInsideAnEfQueryIsSuppressed()
        {
            await VerifyAsync("rows.GroupBy(r => r.VNum.Value)", "CS8629", "r.VNum", true);
        }

        [TestMethod]
        public async Task PossibleNullArgumentInsideAnEfQueryIsSuppressed()
        {
            await VerifyAsync("rows.Select(r => NameOf(r.Item))", "CS8604", "r.Item", true, "item", "string Program.NameOf(Item item)");
        }

        [DataTestMethod]
        [DataRow("list.Where(r => r.Item.Name == \"a\")")]
        [DataRow("rows.AsEnumerable().Select(r => r.Item.Name.Length)")]
        [DataRow("Run(r => r.Item.Name)")]
        [DataRow("Expression<Func<Row, int>> e = r => r.Item.Name.Length")]
        public async Task DereferenceOutsideAnEfQueryIsStillReported(string statement)
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
