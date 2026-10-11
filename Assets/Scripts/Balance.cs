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
        public string id;               // soldier | tank | drone
        public float health;
        public float speed;             // world units per second
        public int reward;              // coins per kill
        public int leakDamage;          // lives lost when it reaches the base
        public bool air;                // only towers with hitsAir can shoot it
        public float size;              // sprite size in world units
    }

    [Serializable]
    public class TowerConfig
    {
        public string id;               // mg | cannon | aa
        public string label;
        public int cost;
        public float damage, damagePerLevel;
        public float fireRate, fireRatePerLevel;    // shots per second
        public float range, rangePerLevel;
        public float splash;            // 0 = single target
        public bool hitsGround, hitsAir;
        public string projectile;       // bullet | shell | missile
        public float projectileSpeed;
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
