using System;
using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    public enum RunState { Playing, GameOver }

    public enum UpgradeKind { Damage = 0, FireRate = 1, MaxHealth = 2, Range = 3 }

    // Owns the whole run: waves, enemies, tower, projectiles, coins and upgrades.
    public sealed class Game : MonoBehaviour
    {
        public const int StressEnemyCount = 200;
        const int EnemyPrewarm = 300;
        const int ProjectilePrewarm = 200;
        const float SpawnMargin = 0.5f;

        static readonly string[] UpgradeIds = { "damage", "fireRate", "maxHealth", "range" };
        static readonly Color EnemyColor = new Color(0.95f, 0.35f, 0.35f);
        static readonly Color EnemyHurtColor = new Color(0.35f, 0.1f, 0.1f);
        static readonly Color TowerColor = new Color(0.35f, 0.8f, 1f);
        static readonly Color ProjectileColor = new Color(1f, 0.9f, 0.4f);
        static readonly Color RangeColor = new Color(0.35f, 0.8f, 1f, 0.25f);

        public static Game Instance { get; private set; }

        public BalanceData Balance { get; private set; }
        public RunState State { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public int ShotsFired { get; private set; }
        public float Coins { get; private set; }
        public float TowerHealth { get; private set; }
        public float RunTime { get; private set; }
        public bool StressMode { get; set; }

        public int EnemyCount => enemies.Active.Count;
        public int ProjectileCount => projectiles.Active.Count;
        public int PoolGrowth => enemies.Created + projectiles.Created;
        public IReadOnlyList<Enemy> ActiveEnemies => enemies.Active;

        public float Damage => StatValue(UpgradeKind.Damage);
        public float FireRate => StatValue(UpgradeKind.FireRate);
        public float MaxHealth => StatValue(UpgradeKind.MaxHealth);
        public float Range => StatValue(UpgradeKind.Range);

        public event Action RunEnded;
        public event Action RunStarted;

        Camera cam;
        UpgradeConfig[] upgrades;
        readonly int[] upgradeLevels = new int[4];
        Pool<Enemy> enemies;
        Pool<Projectile> projectiles;
        Transform tower, rangeRing;

        float waveTimer, spawnTimer, fireTimer;
        int spawnedThisWave, waveCount;
        float waveHealth, waveSpeed, waveDamage, waveReward;

        public void Init(Camera camera)
        {
            Instance = this;
            cam = camera;
            Balance = Corehold.Balance.Load();

            upgrades = new UpgradeConfig[UpgradeIds.Length];
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                upgrades[i] = Array.Find(Balance.upgrades, u => u.id == UpgradeIds[i]);
                if (upgrades[i] == null)
                    throw new InvalidOperationException($"balance.json has no upgrade with id '{UpgradeIds[i]}'");
            }

            var world = new GameObject("World").transform;
            rangeRing = Shapes.CreateRenderer("RangeRing", Shapes.Ring, RangeColor, 0, world).transform;
            tower = Shapes.CreateRenderer("Tower", Shapes.Circle, TowerColor, 3, world).transform;
            tower.localScale = Vector3.one * (Balance.tower.radius * 2f);

            var enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(world, false);
            float enemySize = Balance.waves.enemySize;
            enemies = new Pool<Enemy>(EnemyPrewarm,
                () =>
                {
                    var r = Shapes.CreateRenderer("Enemy", Shapes.Square, EnemyColor, 1, enemyRoot);
                    r.transform.localScale = Vector3.one * enemySize;
                    return new Enemy { transform = r.transform, renderer = r };
                },
                (e, on) => e.transform.gameObject.SetActive(on),
                e => e.activeIndex, (e, i) => e.activeIndex = i);

            var projectileRoot = new GameObject("Projectiles").transform;
            projectileRoot.SetParent(world, false);
            float projectileSize = Balance.tower.projectileSize;
            projectiles = new Pool<Projectile>(ProjectilePrewarm,
                () =>
                {
                    var r = Shapes.CreateRenderer("Projectile", Shapes.Circle, ProjectileColor, 2, projectileRoot);
                    r.transform.localScale = Vector3.one * projectileSize;
                    return new Projectile { transform = r.transform };
                },
                (p, on) => p.transform.gameObject.SetActive(on),
                p => p.activeIndex, (p, i) => p.activeIndex = i);

            StartRun();
        }

        public void StartRun()
        {
            for (int i = enemies.Active.Count - 1; i >= 0; i--) ReleaseEnemy(enemies.Active[i]);
            projectiles.ReleaseAll();
            Array.Clear(upgradeLevels, 0, upgradeLevels.Length);

            State = RunState.Playing;
            Coins = Balance.run.startCoins;
            Kills = 0;
            ShotsFired = 0;
            RunTime = 0f;
            fireTimer = 0f;
            TowerHealth = MaxHealth;
            UpdateRangeRing();
            BeginWave(1);
            RunStarted?.Invoke();
        }

        // ---------------- Upgrades ----------------

        public int UpgradeLevel(UpgradeKind kind) => upgradeLevels[(int)kind];
        public UpgradeConfig UpgradeInfo(UpgradeKind kind) => upgrades[(int)kind];
        public float StatValue(UpgradeKind kind) => Formulas.UpgradeValue(upgrades[(int)kind], upgradeLevels[(int)kind]);
        public float NextStatValue(UpgradeKind kind) => Formulas.UpgradeValue(upgrades[(int)kind], upgradeLevels[(int)kind] + 1);
        public float UpgradeCost(UpgradeKind kind) => Formulas.UpgradeCost(upgrades[(int)kind], upgradeLevels[(int)kind]);

        public bool IsMaxed(UpgradeKind kind)
        {
            int max = upgrades[(int)kind].maxLevel;
            return max > 0 && upgradeLevels[(int)kind] >= max;
        }

        public bool CanBuy(UpgradeKind kind) =>
            State == RunState.Playing && !IsMaxed(kind) && Coins >= UpgradeCost(kind);

        public bool TryBuy(UpgradeKind kind)
        {
            if (!CanBuy(kind)) return false;
            Coins -= UpgradeCost(kind);
            float oldMaxHealth = MaxHealth;
            upgradeLevels[(int)kind]++;
            if (kind == UpgradeKind.MaxHealth) TowerHealth += MaxHealth - oldMaxHealth;
            if (kind == UpgradeKind.Range) UpdateRangeRing();
            return true;
        }

        // Debug and test hook.
        public void AddCoins(float amount) => Coins += amount;

        // ---------------- Frame loop ----------------

        void Update()
        {
            if (State != RunState.Playing) return;
            float dt = Time.deltaTime;
            RunTime += dt;

            UpdateWaves(dt);
            UpdateEnemies(dt);
            UpdateTower(dt);
            UpdateProjectiles(dt);

            if (TowerHealth <= 0f) EndRun();
        }

        void BeginWave(int n)
        {
            Wave = n;
            var w = Balance.waves;
            waveCount = Formulas.EnemyCount(w, n);
            waveHealth = Formulas.EnemyHealth(w, n);
            waveSpeed = Formulas.EnemySpeed(w, n);
            waveDamage = Formulas.EnemyDamage(w, n);
            waveReward = Formulas.CoinsPerKill(w, n);
            waveTimer = 0f;
            spawnTimer = 0f;
            spawnedThisWave = 0;
        }

        void UpdateWaves(float dt)
        {
            var w = Balance.waves;
            waveTimer += dt;

            if (StressMode)
            {
                while (enemies.Active.Count < StressEnemyCount) SpawnEnemy();
            }
            else
            {
                // Enemies of a wave are spread evenly over its duration.
                float interval = w.duration / waveCount;
                spawnTimer += dt;
                while (spawnedThisWave < waveCount && spawnTimer >= interval)
                {
                    spawnTimer -= interval;
                    spawnedThisWave++;
                    SpawnEnemy();
                }
            }

            if (waveTimer >= w.duration + w.breakBetween) BeginWave(Wave + 1);
        }

        void SpawnEnemy()
        {
            float halfH = cam.orthographicSize + SpawnMargin;
            float halfW = cam.orthographicSize * cam.aspect + SpawnMargin;

            // Pick a uniformly random point on the rectangle just outside the screen.
            float t = UnityEngine.Random.value * 2f * (halfW + halfH);
            Vector2 p;
            if (t < 2f * halfW) p = new Vector2(t - halfW, UnityEngine.Random.value < 0.5f ? halfH : -halfH);
            else p = new Vector2(UnityEngine.Random.value < 0.5f ? halfW : -halfW, t - 2f * halfW - halfH);

            var e = enemies.Get();
            e.position = p;
            e.direction = -p.normalized;
            e.maxHealth = e.health = waveHealth;
            e.speed = waveSpeed;
            e.damagePerSecond = waveDamage;
            e.reward = waveReward;
            e.atTower = false;
            e.renderer.color = EnemyColor;
            e.transform.localPosition = p;
        }

        void UpdateEnemies(float dt)
        {
            float stop = Balance.tower.radius + Balance.waves.enemySize * 0.5f;
            float stopSqr = stop * stop;
            float incoming = 0f;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.atTower)
                {
                    incoming += e.damagePerSecond;
                    continue;
                }
                e.position += e.direction * (e.speed * dt);
                if (e.position.sqrMagnitude <= stopSqr)
                {
                    e.atTower = true;
                    e.position = -e.direction * stop;
                }
                e.transform.localPosition = e.position;
            }
            if (!StressMode) TowerHealth -= incoming * dt;
        }

        void UpdateTower(float dt)
        {
            float interval = 1f / FireRate;
            fireTimer += dt;
            if (fireTimer < interval) return;

            var target = FindTarget();
            if (target == null)
            {
                fireTimer = interval;   // stay ready, fire as soon as something enters range
                return;
            }
            // High fire rates can need several shots in one frame.
            while (fireTimer >= interval)
            {
                fireTimer -= interval;
                Fire(target);
            }
        }

        Enemy FindTarget()
        {
            float best = Range * Range;
            Enemy result = null;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                float d = list[i].position.sqrMagnitude;
                if (d <= best)
                {
                    best = d;
                    result = list[i];
                }
            }
            return result;
        }

        void Fire(Enemy target)
        {
            var p = projectiles.Get();
            p.position = Vector2.zero;
            p.target = target;
            p.targetGeneration = target.generation;
            p.damage = Damage;
            p.transform.localPosition = Vector3.zero;
            ShotsFired++;
        }

        void UpdateProjectiles(float dt)
        {
            float step = Balance.tower.projectileSpeed * dt;
            float hitRadius = Balance.waves.enemySize * 0.5f;
            var list = projectiles.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                var target = p.target;
                if (target.generation != p.targetGeneration)
                {
                    projectiles.Release(p);     // target already died
                    continue;
                }
                Vector2 toTarget = target.position - p.position;
                float dist = toTarget.magnitude;
                if (dist <= hitRadius + step)
                {
                    projectiles.Release(p);
                    HitEnemy(target, p.damage);
                    continue;
                }
                p.position += toTarget * (step / dist);
                p.transform.localPosition = p.position;
            }
        }

        void HitEnemy(Enemy e, float damage)
        {
            e.health -= damage;
            if (e.health <= 0f)
            {
                Kills++;
                Coins += e.reward;
                ReleaseEnemy(e);
                return;
            }
            e.renderer.color = Color.Lerp(EnemyHurtColor, EnemyColor, e.health / e.maxHealth);
        }

        void ReleaseEnemy(Enemy e)
        {
            e.generation++;
            enemies.Release(e);
        }

        void EndRun()
        {
            TowerHealth = 0f;
            State = RunState.GameOver;
            RunEnded?.Invoke();
        }

        void UpdateRangeRing()
        {
            rangeRing.localScale = Vector3.one * (Range * 2f);
        }
    }
}
