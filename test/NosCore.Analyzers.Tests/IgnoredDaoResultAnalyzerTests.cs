using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = NosCore.Analyzers.Test.CSharpAnalyzerVerifier<
    NosCore.Analyzers.Analyzers.IgnoredDaoResultAnalyzer>;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class IgnoredDaoResultAnalyzerTests
    {
        private const string Source = @"
#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using NosCore.Dao.Interfaces;

public class Row {{ }}

public class Store : IDao<Row, int>
{{
    public Task<Row?> TryDeleteAsync(int key) => Task.FromResult<Row?>(null);
    public Task<bool> TryInsertOrUpdateAsync(IEnumerable<Row> rows) => Task.FromResult(true);
    public bool TryOther() => true;
}}

public class Program
{{
    private readonly IDao<Row, int> _dao = new Store();
    private readonly Store _store = new Store();

    public async Task RunAsync()
    {{
        {0}
    }}
}}

namespace NosCore.Dao.Interfaces
{{
    public interface IDao<TDto, in TPk>
    {{
        Task<TDto?> TryDeleteAsync(TPk key);
        Task<bool> TryInsertOrUpdateAsync(IEnumerable<TDto> rows);
    }}
}}";

        [DataTestMethod]
        [DataRow("await _dao.TryInsertOrUpdateAsync(new List<Row>());", "_dao.TryInsertOrUpdateAsync(new List<Row>())", "TryInsertOrUpdateAsync")]
        [DataRow("await _dao.TryDeleteAsync(1);", "_dao.TryDeleteAsync(1)", "TryDeleteAsync")]
        [DataRow("await _store.TryDeleteAsync(1);", "_store.TryDeleteAsync(1)", "TryDeleteAsync")]
        [DataRow("_dao.TryDeleteAsync(1).Wait();", null, null)]
        public async Task AnIgnoredWriteIsReported(string statement, string? invocation, string? method)
        {
            if (invocation == null)
            {
                await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement));
                return;
            }

            var column = 9 + statement.IndexOf(invocation);
            var expected = VerifyCS.Diagnostic().WithSpan(23, column, 23, column + invocation.Length).WithArguments(method!);
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement), expected);
        }

        [DataTestMethod]
        [DataRow("if (!await _dao.TryInsertOrUpdateAsync(new List<Row>())) { return; }")]
        [DataRow("var deleted = await _dao.TryDeleteAsync(1);")]
        [DataRow("_ = await _dao.TryDeleteAsync(1);")]
        [DataRow("_store.TryOther();")]
        public async Task AResultThatIsReadOrDiscardedIsNotReported(string statement)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(Source, statement));
        }
    }
}
