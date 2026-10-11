using NUnit.Framework;
using UnityEngine;

namespace Corehold.Tests
{
    public class FormulaTests
    {
        static readonly string[] EnemyIds = { "soldier", "jeep", "drone", "tank", "apc", "heli", "boss" };
        static readonly string[] TowerIds = { "mg", "cannon", "aa", "flame", "tesla", "mortar", "sniper", "cryo" };

        [Test]
        public void BalanceFile_LoadsWithAllEnemiesTowersAndLevels()
        {
            var b = Balance.Load();
            foreach (var id in EnemyIds) Assert.NotNull(b.Enemy(id), id);
            foreach (var id in TowerIds) Assert.NotNull(b.Tower(id), id);
            Assert.GreaterOrEqual(b.levels.Length, 1);
            foreach (var level in b.levels)
            {
                Assert.GreaterOrEqual(level.path.Length, 2, level.name);
                Assert.Greater(level.spots.Length, 0, level.name);
                Assert.Greater(level.waves.Length, 0, level.name);
                foreach (var wave in level.waves)
                foreach (var g in wave.groups)
                {
                    Assert.NotNull(b.Enemy(g.enemy), $"{level.name}: {g.enemy}");
                    Assert.Greater(g.count, 0);
                    Assert.Greater(g.interval, 0f);
                }
            }
        }

        [Test]
        public void EveryEnemyTypeCanBeShotBySomeTower()
        {
            var b = Balance.Load();
            foreach (var e in b.enemies)
                Assert.IsTrue(System.Array.Exists(b.towers, t => e.air ? t.hitsAir : t.hitsGround), e.id);
        }

        [Test]
        public void LevelGeometry_SpotsClearOfRoadAndOnScreen()
        {
            var b = Balance.Load();
            float roadHalf = b.economy.roadWidth * 0.5f;
            const float spotHalf = 0.475f;
            var all = new System.Collections.Generic.List<LevelConfig>(b.levels) { b.endless.ToLevel() };
            foreach (var level in all)
            {
                var path = new LevelPath(LevelPath.Rounded(level.path));
                var hq = level.path[level.path.Length - 1];
                for (int i = 0; i < level.spots.Length; i++)
                {
                    var s = level.spots[i];
                    string where = $"{level.name} spot {i} {s}";
                    Assert.GreaterOrEqual(path.DistanceTo(s), roadHalf + spotHalf + 0.05f, where + " overlaps the road");
                    Assert.GreaterOrEqual((s - hq).magnitude, 0.8f + spotHalf, where + " overlaps the base");
                    Assert.LessOrEqual(Mathf.Abs(s.x) + spotHalf, Bootstrap.WorldHalfWidth, where + " is off screen");
                    Assert.LessOrEqual(s.y + spotHalf, 3.6f, where + " is under the HUD");
                    Assert.GreaterOrEqual(s.y - spotHalf, -4.7f, where + " is off the bottom edge");
                    for (int j = i + 1; j < level.spots.Length; j++)
                        Assert.GreaterOrEqual((s - level.spots[j]).magnitude, spotHalf * 2f, $"{where} overlaps spot {j}");
                }
                for (int i = 1; i < level.path.Length; i++)
                    Assert.LessOrEqual(Mathf.Abs(level.path[i].x) + roadHalf, Bootstrap.WorldHalfWidth, $"{level.name} path point {i} off screen");
            }
        }

        [Test]
        public void TowerAttackKindsAreKnown()
        {
            foreach (var t in Balance.Load().towers)
            {
                Assert.Contains(t.attack, new[] { "projectile", "chain", "instant", "pulse" }, t.id);
                if (t.attack == "projectile") Assert.Greater(t.projectileSpeed, 0f, t.id);
                Assert.IsFalse(string.IsNullOrEmpty(t.role), t.id + " has no role text");
            }
        }

        [Test]
        public void EndlessBudgetAndHealthGrow()
        {
            var b = Balance.Load();
            Assert.Greater(Formulas.EndlessBudget(b.endless, 10), Formulas.EndlessBudget(b.endless, 0));
            Assert.Greater(Formulas.EndlessHealth(b.Enemy("soldier"), b.endless, 10), Formulas.EndlessHealth(b.Enemy("soldier"), b.endless, 0));
            foreach (var u in b.endless.unlocks) Assert.NotNull(b.Enemy(u.enemy));
            Assert.IsTrue(System.Array.Exists(b.enemies, e => e.boss), "endless needs a boss enemy");
        }

        [Test]
        public void UpgradesCostMoreAndGiveMore()
        {
            foreach (var t in Balance.Load().towers)
            {
                Assert.Greater(Formulas.UpgradeCost(t, 1), Formulas.UpgradeCost(t, 0), t.id);
                Assert.Greater(Formulas.TowerDamage(t, 1), Formulas.TowerDamage(t, 0), t.id);
                Assert.GreaterOrEqual(Formulas.TowerRange(t, 1), Formulas.TowerRange(t, 0), t.id);
            }
        }

        [Test]
        public void EnemiesGetTougherEachWave()
        {
            var b = Balance.Load();
            var level = b.levels[0];
            foreach (var e in b.enemies)
                Assert.Greater(Formulas.EnemyHealth(e, level, b.waves, 3), Formulas.EnemyHealth(e, level, b.waves, 0), e.id);
        }
    }
}
