using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NosCore.Analyzers.Suppressors;

namespace NosCore.Analyzers.Tests
{
    [TestClass]
    public class EfNavigationSuppressorTests
    {
        private const string Stubs = @"
namespace Microsoft.EntityFrameworkCore
{
    public class DbContext { }
    public class DbSet<T> where T : class { }
}";

        private static async Task<(string Message, bool Suppressed)[]> SuppressionsAsync(string source)
        {
            var diagnostics = await SuppressorHarness.NullableWarningsAsync(new EfNavigationSuppressor(), Stubs, source);
            return diagnostics.Where(d => d.Id == "CS8618").Select(d => (d.GetMessage(), d.IsSuppressed)).OrderBy(d => d.Item1).ToArray();
        }

        private const string Context = @"
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

public class GameContext : DbContext
{
    public DbSet<Character> Character { get; set; } = new();
    public DbSet<Account> Account { get; set; } = new();
}
";

        [TestMethod]
        public async Task ReferenceNavigationIsSuppressed()
        {
            var result = await SuppressionsAsync(Context + @"
public class Account { }
public class Character
{
    public virtual Account Account { get; set; }
}");
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].Suppressed);
        }

        [TestMethod]
        public async Task ReferenceNavigationOnAnEntityWithAConstructorIsSuppressed()
        {
            var result = await SuppressionsAsync(Context + @"
public class Account { }
public class Character
{
    public Character() { Items = new List<string>(); }
    public virtual Account Account { get; set; }
    public ICollection<string> Items { get; set; }
}");
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].Suppressed);
        }

        [TestMethod]
        public async Task NavigationBetweenEntitiesWithoutADbSetIsSuppressed()
        {
            var result = await SuppressionsAsync(Context + @"
public class Account { }
public class Character
{
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
public class Item
{
    public virtual Character Character { get; set; }
    public virtual Detail Detail { get; set; }
}
public class Detail { }");
            Assert.AreEqual(2, result.Length);
            Assert.IsTrue(result.All(r => r.Suppressed));
        }

        [TestMethod]
        public async Task ScalarPropertyIsStillReported()
        {
            var result = await SuppressionsAsync(Context + @"
public class Account { }
public class Character
{
    public string Name { get; set; }
}");
            Assert.AreEqual(1, result.Length);
            Assert.IsFalse(result[0].Suppressed);
        }

        [TestMethod]
        public async Task NavigationOnANonEntityIsStillReported()
        {
            var result = await SuppressionsAsync(Context + @"
public class Account { }
public class Character { }
public class Dto
{
    public Account Account { get; set; }
}");
            Assert.AreEqual(1, result.Length);
            Assert.IsFalse(result[0].Suppressed);
        }
    }
}
