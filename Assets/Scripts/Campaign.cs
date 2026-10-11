using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Builds the campaign's missions from the rules in balance.json (campaign): a winding road in one of several
    // shapes, pads placed where they cover the most road, and waves that grow with the mission number.
    // Everything is seeded by the mission number, so mission N is always the same.
    public static class Campaign
    {
        // Road bounds (map units): the camera fits CameraRig.PlayArea, the HUD covers the top strip.
        const float XMin = -6.6f, XMax = 6.6f, YTop = 2.8f, YBottom = -3.3f;
        const float PadClearance = 1.1f, PadReach = 2.5f, PadSpacing = 1.3f;
        const float HqClearance = 1.45f;

        static readonly string[][] Names =
        {
            new[] { "Green Valley", "Pine Ridge", "Old Farm", "River Bend", "Meadow Pass", "Fox Hollow", "Stone Bridge", "Oak Hill",
                    "Clover Field", "Willow Creek", "Mill Road", "Shepherd's Gap", "Birch Wood", "Hay Barn", "Quiet Brook",
                    "Long Hedge", "Ridge Camp", "Twin Ponds", "Rook Forest", "Windmill", "Orchard", "Moss Glen", "Kings Road",
                    "Last Meadow", "Valley Gate" },
            new[] { "Dry Wells", "Red Mesa", "Sand Sea", "Scorpion Pass", "Dune Fort", "Sun Gate", "Bone Canyon", "Oasis",
                    "Salt Flats", "Mirage Road", "Cactus Ridge", "Dust Bowl", "Copper Hills", "Viper Trail", "Hot Rocks",
                    "Sphinx Camp", "Rust Yard", "Sandstorm", "Dry Lake", "Old Mine", "Ember Plain", "Camel Track", "Sun Temple",
                    "Glass Desert", "Desert Gate" },
            new[] { "Frost Ridge", "Ice Field", "White Pass", "Glacier", "Polar Camp", "Snow Bridge", "Blizzard Road", "Frozen Lake",
                    "Icicle Gap", "Cold Harbor", "Avalanche", "Wolf Den", "Snow Fort", "Crystal Peak", "Winter Camp", "Hail Valley",
                    "Ice Cave", "Deep Freeze", "Silent Snow", "Aurora", "Frost Bite", "North Wall", "Tundra", "Ice Throne",
                    "Winter Gate" },
            new[] { "Amber Woods", "Red Leaf", "Harvest Road", "Maple Hill", "Rust Forest", "Copper Vale", "Pumpkin Field", "Fog Hollow",
                    "Leaf Fall", "Golden Mill", "Ash Grove", "Crimson Pass", "Bronze Ridge", "Ember Wood", "Old Oak", "Misty Lake",
                    "Hunter's Camp", "Scarlet Glen", "Dusk Road", "Fallen Leaves", "Iron Forest", "Twilight", "Last Harvest",
                    "Final Ridge", "Corehold" },
        };

        public static LevelConfig[] Generate(BalanceData data)
        {
            var c = data.campaign;
            var levels = new LevelConfig[c.levels];
            for (int i = 0; i < c.levels; i++) levels[i] = Mission(data, i + 1);
            return levels;
        }

        public static Biome BiomeOf(CampaignConfig c, int number)
        {
            var biome = Biome.Grass;
            for (int i = 0; i < c.biomes.Length; i++)
                if (number >= c.biomes[i].from) biome = (Biome)i;
            return biome;
        }

        static LevelConfig Mission(BalanceData data, int number)
        {
            var c = data.campaign;
            var random = new System.Random(number * 7919 + 13);
            var biome = BiomeOf(c, number);
            int inBiome = number - c.biomes[(int)biome].from;
            var names = Names[(int)biome];

            Vector2[] path = null, pads = null;
            for (int attempt = 0; attempt < 40 && pads == null; attempt++)
            {
                path = Road(random);
                pads = Pads(path, random, c.pads + random.Next(3) - 1);
            }

            var level = new LevelConfig
            {
                name = names[inBiome % names.Length],
                number = number,
                biome = biome,
                path = path,
                spots = pads,
                healthMultiplier = 1f + c.healthPerLevel * (number - 1),
                startCoins = c.startCoins + c.startCoinsPerLevel * (number - 1),
            };
            foreach (var u in c.enemyIntro) if (u.level == number) level.newEnemy = u.enemy;
            foreach (var u in c.bossIntro) if (u.level == number) level.newEnemy = u.enemy;
            level.waves = Waves(data, level, random);
            level.lives = level.boss != null ? c.bossLives : c.lives;
            return level;
        }

        // ---------------- Roads ----------------

        static float Range(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

        // One of four shapes, randomly mirrored: vertical zig-zag from the side or from the top, or horizontal lanes.
        static Vector2[] Road(System.Random random)
        {
            var pts = new List<Vector2>();
            int shape = random.Next(4);
            float top = Range(random, 2.4f, YTop), bottom = Range(random, YBottom, -2.8f);
            if (shape <= 1)
            {
                // Vertical runs, entered from the left (0) or from the top (1).
                int columns = 3 + random.Next(2);
                bool down = random.Next(2) == 0;
                float x0 = Range(random, -5.6f, -4.6f), x1 = Range(random, 4.8f, XMax);
                for (int i = 0; i < columns; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)(columns - 1)) + Range(random, -0.25f, 0.25f);
                    float from = down ? top : bottom, to = down ? bottom : top;
                    if (i == 0) pts.Add(shape == 0 ? new Vector2(-12f, from) : new Vector2(x, down ? 8f : -8f));
                    if (i == columns - 1) to = Mathf.Lerp(from, to, Range(random, 0.55f, 0.8f));     // the base sits mid-way
                    pts.Add(new Vector2(x, from));
                    pts.Add(new Vector2(x, to));
                    down = !down;
                }
                if (shape == 1) pts.RemoveAt(1);    // entering from the top: the first run starts at the entry
            }
            else
            {
                // Horizontal lanes, three of them, entered from the left (2) or from the top (3).
                var lanes = new[] { top, Range(random, -0.6f, 0.2f), bottom };
                if (random.Next(2) == 0) System.Array.Reverse(lanes);
                float left = Range(random, -6.2f, -5.0f), right = Range(random, 5.4f, XMax);
                if (shape == 2) pts.Add(new Vector2(-12f, lanes[0]));
                else
                {
                    pts.Add(new Vector2(left, lanes[0] > 0 ? 8f : -8f));
                    pts.Add(new Vector2(left, lanes[0]));
                }
                for (int i = 0; i < lanes.Length; i++)
                {
                    bool toRight = i % 2 == 0;
                    float endX = toRight ? right : left;
                    if (i == lanes.Length - 1) endX = toRight ? Range(random, 1.5f, 4.5f) : Range(random, -4.5f, -1.5f);
                    pts.Add(new Vector2(endX, lanes[i]));
                    if (i < lanes.Length - 1) pts.Add(new Vector2(endX, lanes[i + 1]));
                }
            }
            // Mirror so enemies come from either side.
            if (random.Next(2) == 0)
                for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(-pts[i].x, pts[i].y);
            return pts.ToArray();
        }

        // Pads where they see the most road, clear of the road and the base, spread out.
        static Vector2[] Pads(Vector2[] rawPath, System.Random random, int count)
        {
            var path = new LevelPath(LevelPath.Rounded(rawPath));
            var hq = rawPath[rawPath.Length - 1];
            var samples = new List<Vector2>();
            for (float d = 0f; d < path.Length; d += 0.3f)
            {
                int seg = 0;
                var p = path.Sample(d, ref seg, out _);
                if (CameraRig.PlayArea.Contains(p)) samples.Add(p);
            }

            var candidates = new List<(Vector2 p, float score)>();
            for (float y = -4.15f; y <= 3.05f; y += 0.4f)
                for (float x = -7.9f; x <= 7.9f; x += 0.4f)
                {
                    var p = new Vector2(x, y);
                    float dist = path.DistanceTo(p);
                    if (dist < PadClearance || dist > PadReach || (p - hq).magnitude < HqClearance) continue;
                    int seen = 0;
                    foreach (var s in samples) if ((s - p).sqrMagnitude < PadReach * PadReach) seen++;
                    candidates.Add((p, seen + (float)random.NextDouble() * 3f));
                }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));

            var pads = new List<Vector2>();
            foreach (var c in candidates)
            {
                bool free = true;
                foreach (var q in pads) if ((q - c.p).magnitude < PadSpacing) { free = false; break; }
                if (!free) continue;
                pads.Add(c.p);
                if (pads.Count == count) break;
            }
            return pads.Count >= 8 ? pads.ToArray() : null;
        }

        // ---------------- Waves ----------------

        static WaveConfig[] Waves(BalanceData data, LevelConfig level, System.Random random)
        {
            var c = data.campaign;
            int n = level.number;
            var unlocked = new List<string>(c.startEnemies);
            foreach (var u in c.enemyIntro) if (u.level <= n) unlocked.Add(u.enemy);
            var bosses = new List<string>();
            foreach (var u in c.bossIntro) if (u.level <= n) bosses.Add(u.enemy);
            string featured = level.newEnemy != null && !data.Enemy(level.newEnemy).boss ? level.newEnemy : null;

            int count = Mathf.Min(c.maxWaves, c.waves + (n - 1) / c.levelsPerExtraWave);
            var waves = new WaveConfig[count];
            for (int w = 0; w < count; w++)
            {
                float budget = (c.budget + c.budgetPerLevel * (n - 1)) * Mathf.Pow(c.budgetGrowthPerWave, w);
                var groups = new List<SpawnGroup>();

                // Boss missions end with the boss (later on, with two or three of them).
                if (n % 10 == 0 && w == count - 1 && bosses.Count > 0)
                {
                    int bossCount = n >= 100 ? 3 : n >= 70 ? 2 : 1;
                    string first = level.newEnemy != null && data.Enemy(level.newEnemy).boss ? level.newEnemy : bosses[(n / 10 - 1) % bosses.Count];
                    level.boss = first;
                    for (int b = 0; b < bossCount; b++)
                    {
                        var boss = data.Enemy(b == 0 ? first : bosses[(bosses.IndexOf(first) + b) % bosses.Count]);
                        groups.Add(new SpawnGroup { enemy = boss.id, count = 1, interval = 1f, delay = 3f + b * 9f });
                        budget = Mathf.Max(budget * 0.35f, budget - boss.threat);
                    }
                }

                var pool = new List<string>(unlocked);
                int kinds = Mathf.Min(pool.Count, w == 0 ? 1 : 1 + random.Next(3));
                for (int k = 0; k < kinds; k++)
                {
                    string id;
                    if (k == 0 && featured != null && w >= 1) id = featured;
                    else
                    {
                        // Recently unlocked enemies are more likely.
                        int pick = Mathf.Min(pool.Count - 1, (int)(Mathf.Sqrt((float)random.NextDouble()) * pool.Count));
                        id = pool[pick];
                    }
                    pool.Remove(id);
                    var type = data.Enemy(id);
                    float share = id == featured ? 0.5f : 1f / kinds;
                    int amount = Mathf.Max(1, Mathf.RoundToInt(budget * share / type.threat));
                    float interval = Mathf.Clamp(c.spawnInterval * Mathf.Sqrt(type.threat), 0.25f, 3f);
                    groups.Add(new SpawnGroup { enemy = id, count = amount, interval = interval, delay = k * 2.5f });
                    if (pool.Count == 0) break;
                }
                waves[w] = new WaveConfig { groups = groups.ToArray() };
            }
            return waves;
        }
    }
}
