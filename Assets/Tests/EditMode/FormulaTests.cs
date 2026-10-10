using NUnit.Framework;

namespace Corehold.Tests
{
    public class FormulaTests
    {
        [Test]
        public void BalanceFile_LoadsAndHasAllUpgrades()
        {
            var b = Balance.Load();
            Assert.NotNull(b.waves);
            Assert.NotNull(b.tower);
            foreach (var id in new[] { "damage", "fireRate", "maxHealth", "range" })
                Assert.NotNull(System.Array.Find(b.upgrades, u => u.id == id), id);
        }

        [Test]
        public void WavesGetHarder()
        {
            var w = Balance.Load().waves;
            for (int n = 1; n < 30; n++)
            {
                Assert.GreaterOrEqual(Formulas.EnemyCount(w, n + 1), Formulas.EnemyCount(w, n));
                Assert.Greater(Formulas.EnemyHealth(w, n + 1), Formulas.EnemyHealth(w, n));
                Assert.GreaterOrEqual(Formulas.EnemySpeed(w, n + 1), Formulas.EnemySpeed(w, n));
                Assert.GreaterOrEqual(Formulas.CoinsPerKill(w, n), 1f);
            }
        }

        [Test]
        public void UpgradesCostMoreAndGiveMore()
        {
            foreach (var u in Balance.Load().upgrades)
            {
                Assert.Greater(Formulas.UpgradeCost(u, 1), Formulas.UpgradeCost(u, 0), u.id);
                Assert.Greater(Formulas.UpgradeValue(u, 1), Formulas.UpgradeValue(u, 0), u.id);
            }
        }
    }
}
