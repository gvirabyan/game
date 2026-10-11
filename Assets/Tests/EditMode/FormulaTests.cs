using NUnit.Framework;
using UnityEngine;

namespace Corehold.Tests
{
    public class FormulaTests
    {
        static readonly string[] EnemyIds = { "soldier", "jeep", "drone", "tank", "apc", "heli", "boss", "gunship", "titan",
                                              "biker", "medic", "heavy", "hover", "jet", "mech", "colossus", "carrier" };
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
        public void Campaign_HasHundredMissions_NewEnemyEveryFive_BossEveryTen()
        {
            var b = Balance.Load();
            Assert.AreEqual(100, b.levels.Length);
            for (int i = 0; i < b.levels.Length; i++)
            {
                var level = b.levels[i];
                int n = i + 1;
                Assert.AreEqual(n, level.number);
                Assert.GreaterOrEqual(level.spots.Length, 8, $"mission {n} has too few pads");
                // A new enemy every 5 missions up to the last new boss (mission 50); after that every 10 (55, 65, ... 95).
                if ((n % 5 == 0 && n <= 50) || n % 10 == 5) Assert.NotNull(level.newEnemy, $"mission {n} should introduce an enemy");
                if (n % 10 == 0) Assert.NotNull(level.boss, $"mission {n} should have a boss");
                Assert.AreEqual(Campaign.BiomeOf(b.campaign, n), level.biome);
                // A newly introduced enemy actually shows up in its mission.
                if (level.newEnemy != null)
                    Assert.IsTrue(System.Array.Exists(level.waves, w => System.Array.Exists(w.groups, g => g.enemy == level.newEnemy)),
                        $"mission {n} introduces {level.newEnemy} but never sends it");
            }
            Assert.AreEqual(Biome.Grass, b.levels[0].biome);
            Assert.AreEqual(Biome.Autumn, b.levels[99].biome);
        }

        [Test]
        public void Campaign_IsDeterministic()
        {
            var a = Balance.Load();
            var c = Balance.Load();
            for (int i = 0; i < a.levels.Length; i += 7)
            {
                Assert.AreEqual(a.levels[i].path, c.levels[i].path);
                Assert.AreEqual(a.levels[i].spots, c.levels[i].spots);
            }
        }

        [Test]
        public void Localization_EveryKeyInEveryLanguage_WithMatchingPlaceholders()
        {
            var json = Resources.Load<TextAsset>("localization").text;
            var data = JsonUtility.FromJson<LocFile>(json);
            Assert.GreaterOrEqual(data.languages.Length, 15);
            Assert.IsTrue(System.Array.Exists(data.languages, l => l.code == "hy"), "Armenian is missing");
            foreach (var e in data.entries)
            {
                Assert.AreEqual(data.languages.Length, e.values.Length, e.key);
                int placeholders = System.Text.RegularExpressions.Regex.Matches(e.values[0], @"\{\d\}").Count;
                for (int i = 0; i < e.values.Length; i++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(e.values[i]), $"{e.key} is empty in {data.languages[i].code}");
                    Assert.AreEqual(placeholders, System.Text.RegularExpressions.Regex.Matches(e.values[i], @"\{\d\}").Count,
                        $"{e.key} in {data.languages[i].code} has different placeholders");
                }
            }
            // Every tower and enemy has a name and a description.
            var b = Balance.Load();
            foreach (var t in b.towers) Assert.IsTrue(System.Array.Exists(data.entries, e => e.key == "tower." + t.id + ".role"), t.id);
            foreach (var en in b.enemies) Assert.IsTrue(System.Array.Exists(data.entries, e => e.key == "enemy." + en.id + ".info"), en.id);
        }

        [System.Serializable] class LocFile { public LocLanguage[] languages; public LocEntry[] entries; }
        [System.Serializable] class LocLanguage { public string code; }
        [System.Serializable] class LocEntry { public string key; public string[] values; }

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
