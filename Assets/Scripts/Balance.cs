using System;
using UnityEngine;

namespace Corehold
{
    // Every tuning number lives in Assets/Resources/balance.json.
    // This file only describes the shape of that data and the growth formulas.

    [Serializable]
    public class BalanceData
    {
        public RunConfig run;
        public TowerConfig tower;
        public WaveConfig waves;
        public UpgradeConfig[] upgrades;
    }

    [Serializable]
    public class RunConfig
    {
        public float startCoins;
        public float worldHalfWidth;    // visible half-width of the play field in world units
    }

    [Serializable]
    public class TowerConfig
    {
        public float radius;
        public float projectileSpeed;
        public float projectileSize;
    }

    [Serializable]
    public class WaveConfig
    {
        public float duration;          // seconds a wave keeps spawning
        public float breakBetween;      // pause before the next wave starts
        public int baseCount;
        public float countPerWave;
        public int maxCount;
        public float baseHealth;
        public float healthGrowth;
        public float baseSpeed;
        public float speedPerWave;
        public float maxSpeed;
        public float baseDamage;        // damage per second while touching the tower
        public float damageGrowth;
        public float baseCoins;
        public float coinGrowth;
        public float enemySize;
    }

    [Serializable]
    public class UpgradeConfig
    {
        public string id;               // damage | fireRate | maxHealth | range
        public string label;
        public float baseValue;         // stat value at level 0
        public float valuePerLevel;
        public float baseCost;
        public float costGrowth;
        public int maxLevel;            // 0 = unlimited
    }

    // The growth formulas. n is the wave number starting at 1, level starts at 0.
    public static class Formulas
    {
        public static int EnemyCount(WaveConfig w, int n) =>
            Mathf.Min(w.maxCount, w.baseCount + Mathf.FloorToInt(w.countPerWave * (n - 1)));

        public static float EnemyHealth(WaveConfig w, int n) =>
            w.baseHealth * Mathf.Pow(w.healthGrowth, n - 1);

        public static float EnemySpeed(WaveConfig w, int n) =>
            Mathf.Min(w.maxSpeed, w.baseSpeed + w.speedPerWave * (n - 1));

        public static float EnemyDamage(WaveConfig w, int n) =>
            w.baseDamage * Mathf.Pow(w.damageGrowth, n - 1);

        public static float CoinsPerKill(WaveConfig w, int n) =>
            Mathf.Max(1f, Mathf.Round(w.baseCoins * Mathf.Pow(w.coinGrowth, n - 1)));

        public static float UpgradeValue(UpgradeConfig u, int level) =>
            u.baseValue + u.valuePerLevel * level;

        public static float UpgradeCost(UpgradeConfig u, int level) =>
            Mathf.Round(u.baseCost * Mathf.Pow(u.costGrowth, level));
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
