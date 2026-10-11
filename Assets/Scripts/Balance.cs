using System;
using UnityEngine;

namespace Corehold
{
    // Every tuning number and every level layout lives in Assets/Resources/balance.json.
    // This file only describes the shape of that data and the formulas that use it.

    [Serializable]
    public class BalanceData
    {
        public EconomyConfig economy;
        public WaveScaling waves;
        public EnemyConfig[] enemies;
        public TowerConfig[] towers;
        public LevelConfig[] levels;
        public EndlessConfig endless;

        public EnemyConfig Enemy(string id) =>
            Array.Find(enemies, e => e.id == id) ?? throw new InvalidOperationException($"balance.json: unknown enemy '{id}'");

        public TowerConfig Tower(string id) =>
            Array.Find(towers, t => t.id == id) ?? throw new InvalidOperationException($"balance.json: unknown tower '{id}'");
    }

    [Serializable]
    public class EconomyConfig
    {
        public float sellRefund;        // share of everything spent on a tower that selling returns
        public float timeBetweenWaves;  // seconds after a wave finishes spawning before the next starts on its own
        public float firstWaveDelay;    // seconds after the level starts before the first wave starts on its own
        public float roadWidth;
    }

    [Serializable]
    public class WaveScaling
    {
        public float healthGrowth;      // enemy health multiplier per wave inside a level
    }

    [Serializable]
    public class EnemyConfig
    {
        public string id;               // soldier | jeep | drone | tank | apc | heli | boss
        public string label;
        public float health;
        public float speed;             // world units per second
        public int reward;              // coins per kill
        public int leakDamage;          // lives lost when it reaches the base
        public bool air;                // only towers with hitsAir can shoot it
        public float size;              // model size in world units
        public int threat;              // cost in an endless-mode wave budget
        public string spawnOnDeath;     // e.g. an APC drops soldiers when destroyed
        public int spawnCount;
        public bool boss;               // big health bar, camera shake, own model
    }

    [Serializable]
    public class TowerConfig
    {
        public string id;               // mg | cannon | aa | flame | tesla | mortar | sniper | cryo
        public string label;
        public string role;             // one-line description for the build panel
        public int cost;
        public float damage, damagePerLevel;
        public float fireRate, fireRatePerLevel;    // shots per second
        public float range, rangePerLevel;
        public string attack;           // projectile | chain (instant, jumps) | instant (hitscan) | pulse (area around the tower)
        public float splash;            // 0 = single target
        public bool hitsGround, hitsAir;
        public string projectile;       // bullet | shell | missile | flame | mortar
        public float projectileSpeed;
        public string target;           // "" = furthest along the road, "strongest" = most health
        public float minRange;          // cannot hit closer than this (mortar)
        public float burn, burnPerLevel, burnTime;      // damage per second after a hit, for burnTime seconds
        public int chain;               // extra targets a chain attack jumps to (plus one per level)
        public float chainRange;
        public float slow, slowPerLevel, slowTime;      // speed reduction share, for slowTime seconds
        public int upgradeCost;
        public float upgradeCostGrowth;
        public int maxLevel;            // number of upgrades available
    }

    [Serializable]
    public class LevelConfig
    {
        public string name;
        public int lives;
        public int startCoins;
        public float healthMultiplier;  // enemy health multiplier for the whole level
        public Vector2[] path;          // waypoints; the first is extended to the screen edge, the last is the base
        public Vector2[] spots;         // where towers can be built
        public WaveConfig[] waves;
    }

    // Endless mode: one map, waves generated from a growing threat budget.
    [Serializable]
    public class EndlessConfig
    {
        public string name;
        public int lives, startCoins;
        public float budget, budgetGrowth, healthGrowth;
        public int bossEvery;
        public float spawnInterval;
        public EnemyUnlock[] unlocks;
        public Vector2[] path;
        public Vector2[] spots;

        public LevelConfig ToLevel() => new LevelConfig
        {
            name = name, lives = lives, startCoins = startCoins, healthMultiplier = 1f,
            path = path, spots = spots, waves = new WaveConfig[0],
        };
    }

    [Serializable]
    public class EnemyUnlock
    {
        public string enemy;
        public int wave;                // first endless wave (0-based) that may contain it
    }

    [Serializable]
    public class WaveConfig
    {
        public SpawnGroup[] groups;
    }

    [Serializable]
    public class SpawnGroup
    {
        public string enemy;
        public int count;
        public float interval;          // seconds between spawns in this group
        public float delay;             // seconds after the wave starts before the first spawn
    }

    // The growth formulas. waveIndex starts at 0, level (tower upgrade level) starts at 0.
    public static class Formulas
    {
        public static float EnemyHealth(EnemyConfig e, LevelConfig level, WaveScaling w, int waveIndex) =>
            e.health * level.healthMultiplier * Mathf.Pow(w.healthGrowth, waveIndex);

        public static float TowerDamage(TowerConfig t, int level) => t.damage + t.damagePerLevel * level;
        public static float TowerFireRate(TowerConfig t, int level) => t.fireRate + t.fireRatePerLevel * level;
        public static float TowerRange(TowerConfig t, int level) => t.range + t.rangePerLevel * level;

        public static int UpgradeCost(TowerConfig t, int level) =>
            Mathf.RoundToInt(t.upgradeCost * Mathf.Pow(t.upgradeCostGrowth, level));

        public static int SellValue(int totalSpent, EconomyConfig e) =>
            Mathf.FloorToInt(totalSpent * e.sellRefund);

        public static float Burn(TowerConfig t, int level) => t.burn + t.burnPerLevel * level;
        public static int ChainTargets(TowerConfig t, int level) => t.chain + level;
        public static float Slow(TowerConfig t, int level) => Mathf.Clamp(t.slow + t.slowPerLevel * level, 0f, 0.9f);

        public static float EndlessBudget(EndlessConfig e, int waveIndex) => e.budget * Mathf.Pow(e.budgetGrowth, waveIndex);
        public static float EndlessHealth(EnemyConfig enemy, EndlessConfig e, int waveIndex) =>
            enemy.health * Mathf.Pow(e.healthGrowth, waveIndex);
    }

    public static class Balance
    {
        public static BalanceData Load()
        {
            var asset = Resources.Load<TextAsset>("balance");
            if (asset == null)
                throw new InvalidOperationException("Assets/Resources/balance.json is missing");
            return JsonUtility.FromJson<BalanceData>(asset.text);
        }
    }
}
