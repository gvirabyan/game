using NUnit.Framework;
using UnityEngine;

namespace Corehold.Tests
{
    public class FormulaTests
    {
        static readonly string[] EnemyIds = { "soldier", "tank", "drone" };
        static readonly string[] TowerIds = { "mg", "cannon", "aa" };

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
            foreach (var level in b.levels)
            {
                var path = new LevelPath(level.path);
                var hq = level.path[level.path.Length - 1];
                for (int i = 0; i < level.spots.Length; i++)
                {
                    var s = level.spots[i];
                    string where = $"{level.name} spot {i} {s}";
                    Assert.GreaterOrEqual(path.DistanceTo(s), roadHalf + spotHalf + 0.05f, where + " overlaps the road");
                    Assert.GreaterOrEqual((s - hq).magnitude, 0.8f + spotHalf, where + " overlaps the base");
                    Assert.LessOrEqual(Mathf.Abs(s.x) + spotHalf, Bootstrap.WorldHalfWidth, where + " is off screen");
                    Assert.LessOrEqual(s.y + spotHalf, 7.4f, where + " is under the HUD");
                    Assert.GreaterOrEqual(s.y - spotHalf, -7.0f, where + " is under the bottom bar");
                    for (int j = i + 1; j < level.spots.Length; j++)
                        Assert.GreaterOrEqual((s - level.spots[j]).magnitude, spotHalf * 2f, $"{where} overlaps spot {j}");
                }
                for (int i = 1; i < level.path.Length; i++)
                    Assert.LessOrEqual(Mathf.Abs(level.path[i].x) + roadHalf, Bootstrap.WorldHalfWidth, $"{level.name} path point {i} off screen");
            }
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
