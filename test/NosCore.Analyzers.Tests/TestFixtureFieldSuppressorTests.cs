using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NosCore.Analyzers.Suppressors;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class TestFixtureFieldSuppressorTests
    {
        private const string Stubs = @"
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    public sealed class TestClassAttribute : System.Attribute { }
    public sealed class TestInitializeAttribute : System.Attribute { }
    public sealed class TestMethodAttribute : System.Attribute { }
}";

        private static async Task<bool[]> SuppressionsAsync(string source)
        {
            var diagnostics = await SuppressorHarness.NullableWarningsAsync(new TestFixtureFieldSuppressor(), Stubs, source);
            return diagnostics.Where(d => d.Id == "CS8618").Select(d => d.IsSuppressed).ToArray();
        }

        [TestMethod]
        public async Task FieldAssignedInTestInitializeIsSuppressed()
        {
            var result = await SuppressionsAsync(@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public class Tests
{
    private string _name;
    public string Title { get; set; }

    [TestInitialize]
    public async System.Threading.Tasks.Task SetupAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        _name = ""a"";
        Title = _name;
    }
}");
            CollectionAssert.AreEqual(new[] { true, true }, result);
        }

        [TestMethod]
        public async Task FieldAssignedInABaseTestInitializeIsSuppressed()
        {
            var result = await SuppressionsAsync(@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
public abstract class TestBase
{
    protected string Name;

    [TestInitialize]
    public void Setup() => Name = ""a"";
}

[TestClass]
public class Tests : TestBase
{
}");
            CollectionAssert.AreEqual(new[] { true }, result);
        }

        [TestMethod]
        public async Task FieldWithAnExplicitConstructorIsSuppressed()
        {
            var result = await SuppressionsAsync(@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public class Tests
{
    private string _name;
    private readonly int _count;

    public Tests() => _count = 1;

    [TestInitialize]
    public void Setup() => _name = ""a"";
}");
            CollectionAssert.AreEqual(new[] { true }, result);
        }

        [TestMethod]
        public async Task FieldAssignedInATestStepIsSuppressed()
        {
            var result = await SuppressionsAsync(@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public class Tests
{
    private string _name;
    private int _count;
    private string _parsed;

    [TestMethod]
    public void Works()
    {
        GivenAName();
        int.TryParse(_name, out _count);
    }

    private void GivenAName() => _name = ""a"";

    private string Parsed { set => _parsed = value; }
}");
            CollectionAssert.AreEqual(new[] { true, true }, result);
        }

        [TestMethod]
        public async Task FieldNeverAssignedInATestClassIsStillReported()
        {
            var result = await SuppressionsAsync(@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public class Tests
{
    private string _name;
    private string _other;

    [TestInitialize]
    public void Setup() => _other = ""a"";

    public string Read() => _name;
}");
            CollectionAssert.AreEqual(new[] { false, true }, result);
        }

        [TestMethod]
        public async Task FieldOutsideATestClassIsStillReported()
        {
            var result = await SuppressionsAsync(@"
public class Helper
{
    private string _name;

    public void Setup() => _name = ""a"";
}");
            CollectionAssert.AreEqual(new[] { false }, result);
        }
    }
}
