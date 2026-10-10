using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Corehold
{
    public enum GameState { Menu, Playing, Won, Lost }

    // Owns a level: path, build spots, towers, waves, enemies, projectiles, effects, coins and lives.
    // Logic runs in flat map coordinates; see View for how they map to the 2.5D world.
    public sealed class Game : MonoBehaviour
    {
        public const int StressEnemyCount = 200;
        const int EnemyPrewarm = 260;
        const int ProjectilePrewarm = 200;
        const int EffectPrewarm = 80;
        const float TurretTurnSpeed = 540f;
        const float EnemyTurnSpeed = 360f;
        const float HitRadius = 0.12f;
        const float HealthBarWidth = 0.6f;
        const float DroneHeight = 1.3f;
        const float TowerTop = 0.32f;          // turret mount height on the tower base
        const float MuzzleHeight = 0.45f;

        public static Game Instance { get; private set; }

        public BalanceData Balance { get; private set; }
        public GameState State { get; private set; }
        public int LevelIndex { get; private set; }
        public LevelConfig Level { get; private set; }
        public int Coins { get; private set; }
        public int Lives { get; private set; }
        public int Kills { get; private set; }
        public int WavesStarted { get; private set; }
        public int WaveCount => Level != null ? Level.waves.Length : 0;
        public bool Spawning { get; private set; }
        public float NextWaveTimer { get; private set; }
        public bool StressMode { get; set; }
        public BuildSpot Selected { get; private set; }
        public IReadOnlyList<BuildSpot> Spots => spots;

        public int EnemyCount => enemies.Active.Count;
        public int ProjectileCount => projectiles.Active.Count;
        public int PoolGrowth => enemies.Created + projectiles.Created + effects.Created;
        public bool CanStartNextWave => State == GameState.Playing && !Spawning && WavesStarted < WaveCount;

        public event Action StateChanged;
        public event Action SelectionChanged;

        Camera cam;
        Quaternion billboard;
        LevelPath path;
        Transform levelRoot, world, overlay;
        readonly List<BuildSpot> spots = new List<BuildSpot>();
        Pool<Enemy> enemies;
        Pool<Projectile> projectiles;
        Pool<Effect> effects;
        SpriteRenderer rangeFill, rangeRing;

        // Spawning state of the current wave.
        readonly List<SpawnGroup> groups = new List<SpawnGroup>();
        readonly List<int> groupRemaining = new List<int>();
        readonly List<float> groupTimer = new List<float>();
        int stressSpawnCycle;

        public void Init(Camera camera)
        {
            Instance = this;
            cam = camera;
            billboard = cam.transform.rotation;
            Balance = Corehold.Balance.Load();

            // 3D models live under the stretched world root; billboards (health bars, effects) live outside it.
            world = new GameObject("World").transform;
            world.localScale = new Vector3(1f, 1f, View.ZStretch);
            overlay = new GameObject("Overlay").transform;
            CreatePools();

            rangeFill = FlatSprite("RangeFill", Art.Disc, new Color(1f, 1f, 1f, 0.12f));
            rangeRing = FlatSprite("RangeRing", Art.Ring, new Color(1f, 1f, 1f, 0.7f));
            ShowRange(Vector2.zero, 0f);

            ShowMenu();
        }

        // ---------------- Level flow ----------------

        // The menu shows the first level as a live backdrop.
        public void ShowMenu()
        {
            ClearLevel();
            Time.timeScale = 1f;
            var preview = Balance.levels[0];
            var view = ViewRect();
            levelRoot = LevelView.Build(preview, new LevelPath(ExtendStartOffscreen(preview.path, view)),
                Balance.economy.roadWidth, view, 1000, world, new List<BuildSpot>());
            State = GameState.Menu;
            StateChanged?.Invoke();
        }

        public void LoadLevel(int index)
        {
            ClearLevel();
            LevelIndex = Mathf.Clamp(index, 0, Balance.levels.Length - 1);
            Level = Balance.levels[LevelIndex];

            var view = ViewRect();
            path = new LevelPath(ExtendStartOffscreen(Level.path, view));
            levelRoot = LevelView.Build(Level, path, Balance.economy.roadWidth, view, 1000 + LevelIndex * 97, world, spots);

            Coins = Level.startCoins;
            Lives = Level.lives;
            Kills = 0;
            WavesStarted = 0;
            Spawning = false;
            NextWaveTimer = 0f;
            State = GameState.Playing;
            StateChanged?.Invoke();
        }

        public void RestartLevel() => LoadLevel(LevelIndex);

        public void NextLevel() => LoadLevel((LevelIndex + 1) % Balance.levels.Length);

        void ClearLevel()
        {
            Deselect();
            enemies.ReleaseAll();
            projectiles.ReleaseAll();
            effects.ReleaseAll();
            spots.Clear();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            Level = null;
            StressMode = false;
        }

        // Visible map area. The camera is orthographic and the world is stretched so map units equal screen units.
        Rect ViewRect()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            return new Rect(-halfW, -halfH, halfW * 2f, halfH * 2f);
        }

        // Enemies should walk in from beyond the screen edge on every aspect ratio.
        static Vector2[] ExtendStartOffscreen(Vector2[] source, Rect view)
        {
            var pts = (Vector2[])source.Clone();
            var dir = (pts[0] - pts[1]).normalized;
            var outer = new Rect(view.xMin - 1f, view.yMin - 1f, view.width + 2f, view.height + 2f);
            while (outer.Contains(pts[0])) pts[0] += dir * 0.5f;
            return pts;
        }

        // ---------------- Waves ----------------

        public void StartNextWave()
        {
            if (!CanStartNextWave) return;
            var wave = Level.waves[WavesStarted];
            groups.Clear();
            groupRemaining.Clear();
            groupTimer.Clear();
            foreach (var g in wave.groups)
            {
                Balance.Enemy(g.enemy);     // fail fast on typos in balance.json
                groups.Add(g);
                groupRemaining.Add(g.count);
                groupTimer.Add(g.delay);
            }
            WavesStarted++;
            Spawning = true;
            StateChanged?.Invoke();
        }

        void UpdateWaves(float dt)
        {
            if (StressMode)
            {
                var types = Balance.enemies;
                while (enemies.Active.Count < StressEnemyCount)
                    SpawnEnemy(types[stressSpawnCycle++ % types.Length], Mathf.Max(0, WavesStarted - 1), true);
            }

            if (Spawning)
            {
                bool any = false;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groupRemaining[i] <= 0) continue;
                    any = true;
                    groupTimer[i] -= dt;
                    var type = Balance.Enemy(groups[i].enemy);
                    while (groupTimer[i] <= 0f && groupRemaining[i] > 0)
                    {
                        SpawnEnemy(type, WavesStarted - 1, false);
                        groupRemaining[i]--;
                        groupTimer[i] += groups[i].interval;
                    }
                }
                if (!any)
                {
                    Spawning = false;
                    NextWaveTimer = Balance.economy.timeBetweenWaves;
                    StateChanged?.Invoke();
                }
            }
            else if (WavesStarted > 0 && WavesStarted < WaveCount)
            {
                NextWaveTimer -= dt;
                if (NextWaveTimer <= 0f) StartNextWave();
            }
        }

        // ---------------- Enemies ----------------

        void SpawnEnemy(EnemyConfig type, int waveIndex, bool spreadAlongPath)
        {
            var e = enemies.Get();
            e.type = type;
            e.maxHealth = e.health = Formulas.EnemyHealth(type, Level, Balance.waves, waveIndex);
            e.distance = spreadAlongPath ? UnityEngine.Random.value * path.Length * 0.9f : 0f;
            e.segment = 0;
            e.position = path.Sample(e.distance, ref e.segment, out var dir);
            e.angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            e.height = type.air ? DroneHeight : 0f;

            e.filter.sharedMesh = type.id == "tank" ? Models.Tank : type.air ? Models.Drone : Models.Soldier;
            e.root.localScale = Vector3.one * type.size;
            e.root.localPosition = View.Local(e.position, e.height);
            e.root.localRotation = View.Yaw(e.angle);
            SetHealthBar(e);
        }

        void UpdateEnemies(float dt)
        {
            float time = Time.time;
            var list = enemies.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                e.distance += e.type.speed * dt;
                if (e.distance >= path.Length)
                {
                    Leak(e);
                    continue;
                }
                e.position = path.Sample(e.distance, ref e.segment, out var dir);
                float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                e.angle = Mathf.MoveTowardsAngle(e.angle, target, EnemyTurnSpeed * dt);

                // Cheap life: soldiers bob as they walk, drones hover.
                float bob = e.type.air
                    ? Mathf.Sin(time * 3f + e.activeIndex) * 0.08f
                    : e.type.id == "soldier" ? Mathf.Abs(Mathf.Sin(e.distance * 9f)) * 0.05f : 0f;
                e.root.localPosition = View.Local(e.position, e.height + bob);
                e.root.localRotation = View.Yaw(e.angle);
                if (e.hpRoot.gameObject.activeSelf) e.hpRoot.position = View.World(e.position, e.height + e.type.size * 1.3f + 0.25f);
            }
        }

        void Leak(Enemy e)
        {
            if (StressMode)
            {
                // Keep stress enemies walking: send them back to the start instead of removing them.
                e.distance = 0f;
                e.segment = 0;
                return;
            }
            Lives = Mathf.Max(0, Lives - e.type.leakDamage);
            SpawnEffect(e.position, 0.8f, 1.4f, Art.Palette.Danger);
            ReleaseEnemy(e);
        }

        void Damage(Enemy e, float amount)
        {
            e.health -= amount;
            if (e.health > 0f)
            {
                SetHealthBar(e);
                return;
            }
            Kills++;
            Coins += e.type.reward;
            SpawnEffect(e.position, e.height + 0.3f, e.type.size * 2f, Art.Palette.Explosion);
            ReleaseEnemy(e);
        }

        void SetHealthBar(Enemy e)
        {
            float frac = Mathf.Clamp01(e.health / e.maxHealth);
            bool show = frac < 0.999f;
            if (e.hpRoot.gameObject.activeSelf != show) e.hpRoot.gameObject.SetActive(show);
            if (!show) return;
            float w = HealthBarWidth * frac;
            e.hpFill.localScale = new Vector3(w, 0.08f, 1f);
            e.hpFill.localPosition = new Vector3(-HealthBarWidth * 0.5f + w * 0.5f, 0f, -0.01f);
            e.hpFillRenderer.color = frac > 0.5f ? Art.Palette.Good : frac > 0.25f ? Art.Palette.Gold : Art.Palette.Danger;
            e.hpRoot.position = View.World(e.position, e.height + e.type.size * 1.3f + 0.25f);
        }

        void ReleaseEnemy(Enemy e)
        {
            e.generation++;
            enemies.Release(e);
        }

        static bool CanHit(TowerConfig t, Enemy e) => e.type.air ? t.hitsAir : t.hitsGround;

        // ---------------- Towers ----------------

        public void Select(BuildSpot spot)
        {
            Selected = spot;
            if (spot != null)
            {
                float range = spot.tower != null
                    ? Formulas.TowerRange(spot.tower.type, spot.tower.level)
                    : 0.62f;                    // just highlight an empty pad
                ShowRange(spot.position, range);
            }
            else ShowRange(Vector2.zero, 0f);
            SelectionChanged?.Invoke();
        }

        public void Select(int spotIndex) => Select(spots[spotIndex]);

        public void Deselect()
        {
            if (Selected == null && !rangeRing.enabled) return;
            Select((BuildSpot)null);
        }

        public void ShowRange(Vector2 center, float range)
        {
            bool on = range > 0f;
            rangeFill.enabled = rangeRing.enabled = on;
            if (!on) return;
            var pos = View.Local(center, 0.12f);
            rangeFill.transform.localPosition = rangeRing.transform.localPosition = pos;
            rangeFill.transform.localScale = rangeRing.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
        }

        // A sprite lying flat on the ground under the stretched root, so it reads as a circle on screen.
        SpriteRenderer FlatSprite(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = Layers.Range;
            return r;
        }

        public bool CanBuild(string towerId) =>
            State == GameState.Playing && Selected != null && Selected.tower == null && Coins >= Balance.Tower(towerId).cost;

        public bool TryBuild(string towerId)
        {
            if (!CanBuild(towerId)) return false;
            var type = Balance.Tower(towerId);
            var spot = Selected;
            Coins -= type.cost;

            var tower = new Tower { type = type, spent = type.cost, angle = 90f };
            tower.root = new GameObject("Tower_" + type.id);
            tower.root.transform.SetParent(levelRoot, false);
            tower.root.transform.localPosition = View.Local(spot.position, 0.1f);
            MeshObject("Base", Models.TowerBase, tower.root.transform);
            var turretMesh = type.id == "cannon" ? Models.CannonTurret : type.id == "aa" ? Models.AaTurret : Models.MgTurret;
            tower.turret = MeshObject("Turret", turretMesh, tower.root.transform);
            tower.turret.localPosition = new Vector3(0f, TowerTop, 0f);
            tower.turret.localRotation = View.Yaw(tower.angle);
            tower.pips = new GameObject[type.maxLevel];
            for (int i = 0; i < type.maxLevel; i++)
            {
                float a = (200f + i * 35f) * Mathf.Deg2Rad;
                var pip = MeshObject("Pip", Models.Pip, tower.root.transform);
                pip.localPosition = new Vector3(Mathf.Cos(a) * 0.4f, 0.22f, Mathf.Sin(a) * 0.4f);
                tower.pips[i] = pip.gameObject;
                pip.gameObject.SetActive(false);
            }

            spot.tower = tower;
            SpawnEffect(spot.position, 0.4f, 1.3f, Art.Palette.Accent);
            Select(spot);
            return true;
        }

        public bool CanUpgrade =>
            State == GameState.Playing && Selected?.tower != null &&
            Selected.tower.level < Selected.tower.type.maxLevel &&
            Coins >= Formulas.UpgradeCost(Selected.tower.type, Selected.tower.level);

        public bool TryUpgrade()
        {
            if (!CanUpgrade) return false;
            var t = Selected.tower;
            int cost = Formulas.UpgradeCost(t.type, t.level);
            Coins -= cost;
            t.spent += cost;
            t.pips[t.level].SetActive(true);
            t.level++;
            SpawnEffect(Selected.position, 0.5f, 1.1f, Art.Palette.Gold);
            Select(Selected);
            return true;
        }

        public bool TrySell()
        {
            if (State != GameState.Playing || Selected?.tower == null) return false;
            var spot = Selected;
            Coins += Formulas.SellValue(spot.tower.spent, Balance.economy);
            Destroy(spot.tower.root);
            spot.tower = null;
            Select(spot);
            return true;
        }

        void UpdateTowers(float dt)
        {
            for (int s = 0; s < spots.Count; s++)
            {
                var t = spots[s].tower;
                if (t == null) continue;
                var pos = spots[s].position;
                float range = Formulas.TowerRange(t.type, t.level);

                if (!IsValidTarget(t, t.target, t.targetGeneration, pos, range))
                {
                    t.target = FindTarget(t.type, pos, range);
                    t.targetGeneration = t.target != null ? t.target.generation : 0;
                }

                t.cooldown -= dt;
                if (t.target == null)
                {
                    if (t.cooldown < 0f) t.cooldown = 0f;
                    continue;
                }

                var to = t.target.position - pos;
                float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                t.angle = Mathf.MoveTowardsAngle(t.angle, want, TurretTurnSpeed * dt);
                t.turret.localRotation = View.Yaw(t.angle);

                float interval = 1f / Formulas.TowerFireRate(t.type, t.level);
                for (int shots = 0; t.cooldown <= 0f && shots < 4; shots++)
                {
                    Fire(t, pos, t.target);
                    t.cooldown += interval;
                }
            }
        }

        static bool IsValidTarget(Tower t, Enemy e, int generation, Vector2 pos, float range) =>
            e != null && e.generation == generation && (e.position - pos).sqrMagnitude <= range * range;

        // Target the enemy furthest along the path, i.e. the one closest to the base.
        Enemy FindTarget(TowerConfig type, Vector2 pos, float range)
        {
            float r2 = range * range;
            Enemy best = null;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!CanHit(type, e)) continue;
                if ((e.position - pos).sqrMagnitude > r2) continue;
                if (best == null || e.distance > best.distance) best = e;
            }
            return best;
        }

        void Fire(Tower t, Vector2 pos, Enemy target)
        {
            var p = projectiles.Get();
            float rad = t.angle * Mathf.Deg2Rad;
            p.position = pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 0.45f;
            p.height = MuzzleHeight;
            p.source = t;
            p.damage = Formulas.TowerDamage(t.type, t.level);
            p.target = target;
            p.targetGeneration = target.generation;
            p.aim = target.position;

            p.filter.sharedMesh = t.type.projectile == "shell" ? Models.Shell
                : t.type.projectile == "missile" ? Models.Missile : Models.Bullet;
            p.transform.localPosition = View.Local(p.position, p.height);
            p.transform.localRotation = View.Yaw(t.angle);
        }

        void UpdateProjectiles(float dt)
        {
            var list = projectiles.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                var type = p.source.type;
                bool targetAlive = p.target.generation == p.targetGeneration;
                if (targetAlive) p.aim = p.target.position;
                else if (type.splash <= 0f)
                {
                    projectiles.Release(p);         // bullet or missile lost its target
                    continue;
                }

                var to = p.aim - p.position;
                float dist = to.magnitude;
                float step = type.projectileSpeed * dt;
                float aimHeight = targetAlive ? p.target.height + 0.25f : 0.1f;
                if (dist <= step + HitRadius)
                {
                    projectiles.Release(p);
                    Impact(type, p.aim, aimHeight, p.damage, targetAlive ? p.target : null);
                    continue;
                }
                // Climb or drop toward the target height over the remaining distance.
                p.height = Mathf.Lerp(p.height, aimHeight, step / dist);
                p.position += to * (step / dist);
                p.transform.localPosition = View.Local(p.position, p.height);
                p.transform.localRotation = View.Yaw(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            }
        }

        void Impact(TowerConfig type, Vector2 at, float height, float damage, Enemy target)
        {
            if (type.splash > 0f)
            {
                float r2 = type.splash * type.splash;
                var list = enemies.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var e = list[i];
                    if (CanHit(type, e) && (e.position - at).sqrMagnitude <= r2) Damage(e, damage);
                }
                SpawnEffect(at, 0.3f, type.splash * 2.2f, Art.Palette.Explosion);
                return;
            }
            if (target == null) return;
            if (type.projectile == "missile") SpawnEffect(at, height, 0.9f, Art.Palette.Explosion);
            Damage(target, damage);
        }

        // ---------------- Effects ----------------

        void SpawnEffect(Vector2 at, float height, float size, Color color)
        {
            var fx = effects.Get();
            fx.age = 0f;
            fx.life = 0.35f;
            fx.size = size;
            fx.color = color;
            fx.transform.position = View.World(at, height);
            fx.transform.localScale = Vector3.one * (size * 0.4f);
            fx.renderer.color = color;
        }

        void UpdateEffects(float dt)
        {
            var list = effects.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var fx = list[i];
                fx.age += dt;
                float t = fx.age / fx.life;
                if (t >= 1f)
                {
                    effects.Release(fx);
                    continue;
                }
                fx.transform.localScale = Vector3.one * (fx.size * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(t)));
                var c = fx.color;
                c.a = 1f - t;
                fx.renderer.color = c;
            }
        }

        // ---------------- Frame loop ----------------

        void Update()
        {
            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;

            HandleTap();
            UpdateWaves(dt);
            UpdateEnemies(dt);
            UpdateTowers(dt);
            UpdateProjectiles(dt);
            UpdateEffects(dt);

            if (Lives <= 0)
                Finish(GameState.Lost);
            else if (!StressMode && WavesStarted == WaveCount && !Spawning && enemies.Active.Count == 0)
                Finish(GameState.Won);
        }

        void Finish(GameState result)
        {
            Deselect();
            State = result;
            Time.timeScale = 1f;
            StateChanged?.Invoke();
        }

        void HandleTap()
        {
            if (!Input.GetMouseButtonDown(0) || PointerOverUI()) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
            var hit = ray.GetPoint(enter);
            var p = new Vector2(hit.x, hit.z / View.ZStretch);

            BuildSpot picked = null;
            float best = LevelView.SpotPickRadius * LevelView.SpotPickRadius;
            foreach (var s in spots)
            {
                // Towers stand up from their pad, so also accept taps slightly above it on screen.
                var d = s.position - p;
                if (s.tower != null && d.y < 0f) d.y = Mathf.Min(0f, d.y + 0.35f);
                float d2 = d.sqrMagnitude;
                if (d2 <= best) { best = d2; picked = s; }
            }
            if (picked != null && picked != Selected) Select(picked);
            else Deselect();
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            for (int i = 0; i < Input.touchCount; i++)
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return es.IsPointerOverGameObject();
        }

        // Debug and test hook.
        public void AddCoins(int amount) => Coins += amount;

        // ---------------- Pools ----------------

        static Transform MeshObject(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;
            return go.transform;
        }

        SpriteRenderer Billboard(string name, Sprite sprite, Color color, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = billboard;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        void CreatePools()
        {
            var enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(world, false);
            var barRoot = new GameObject("HealthBars").transform;
            barRoot.SetParent(overlay, false);
            enemies = new Pool<Enemy>(EnemyPrewarm, () =>
            {
                var root = MeshObject("Enemy", Models.Soldier, enemyRoot);
                var hp = new GameObject("HealthBar").transform;
                hp.SetParent(barRoot, false);
                hp.rotation = billboard;
                var e = new Enemy { root = root, filter = root.GetComponent<MeshFilter>(), hpRoot = hp };
                e.hpBackRenderer = Billboard("Back", Art.Pixel, new Color(0f, 0f, 0f, 0.65f), Layers.HealthBar, hp);
                e.hpBackRenderer.transform.localScale = new Vector3(HealthBarWidth + 0.05f, 0.13f, 1f);
                e.hpFillRenderer = Billboard("Fill", Art.Pixel, Art.Palette.Good, Layers.HealthBar + 1, hp);
                e.hpFill = e.hpFillRenderer.transform;
                hp.gameObject.SetActive(false);
                return e;
            },
            (e, on) =>
            {
                e.root.gameObject.SetActive(on);
                if (!on) e.hpRoot.gameObject.SetActive(false);
            },
            e => e.activeIndex, (e, i) => e.activeIndex = i);

            var projectileRoot = new GameObject("Projectiles").transform;
            projectileRoot.SetParent(world, false);
            projectiles = new Pool<Projectile>(ProjectilePrewarm, () =>
            {
                var t = MeshObject("Projectile", Models.Bullet, projectileRoot);
                var r = t.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                return new Projectile { transform = t, filter = t.GetComponent<MeshFilter>() };
            },
            (p, on) => p.transform.gameObject.SetActive(on),
            p => p.activeIndex, (p, i) => p.activeIndex = i);

            var effectRoot = new GameObject("Effects").transform;
            effectRoot.SetParent(overlay, false);
            effects = new Pool<Effect>(EffectPrewarm, () =>
            {
                var r = Billboard("Effect", Art.Glow, Color.white, Layers.Effect, effectRoot);
                return new Effect { transform = r.transform, renderer = r };
            },
            (fx, on) => fx.transform.gameObject.SetActive(on),
            fx => fx.activeIndex, (fx, i) => fx.activeIndex = i);
        }
    }
}
