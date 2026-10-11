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
        const int EffectPrewarm = 240;
        const float TurretTurnSpeed = 540f;
        const float EnemyTurnSpeed = 360f;
        const float HitRadius = 0.12f;
        const float HealthBarWidth = 0.6f;
        const float DroneHeight = 1.3f;
        const float TowerTop = 0.32f;          // turret mount height on the tower base
        const float MuzzleHeight = 0.45f;
        const float UnitScale = 1.3f;
        const float TurretScanSpeed = 20f;     // idle turrets slowly sweep
        const float RecoilDistance = 0.07f, RecoilRecovery = 7f;
        const int Soldier = 0, Tank = 1, Drone = 2;
        const float MaxZoom = 2.2f;
        const float DragThreshold = 0.025f;    // fraction of screen height a press may move and still count as a tap
        const float BounceTime = 0.45f;          // models are drawn larger than their logic size for readability
        const float FlashFade = 8f;            // enemy hit flash fades in 1/8 s
        static readonly Color SmokeColor = new Color(0.42f, 0.42f, 0.4f, 0.55f);
        static readonly Color MuzzleColor = Art.Hex("FFE9A8"), SparkColor = Art.Hex("FFD27A"), FireCoreColor = Art.Hex("FFF4D6");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

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
        public float Zoom { get => zoom; set => zoom = Mathf.Clamp(value, 1f, MaxZoom); }
        public int ProjectileCount => projectiles.Active.Count;
        public int PoolGrowth => enemies.Created + projectiles.Created + effects.Created;
        public bool CanStartNextWave => State == GameState.Playing && !Spawning && WavesStarted < WaveCount;

        public event Action StateChanged;
        public event Action SelectionChanged;

        Camera cam;
        Vector3 camHome;
        float shake;
        // Pinch zoom and drag pan on top of the fitted camera.
        float zoom = 1f, groundDistance, pinchStartDistance, pinchStartZoom;
        Vector3 pan;
        Vector2 visibleSize;                   // world size of the ground seen at zoom 1 (x, z)
        Vector2 pressPosition, lastPointer;
        bool pressing, dragged, pinching;
        MaterialPropertyBlock flashBlock;
        Quaternion billboard;
        LevelPath path;
        Transform levelRoot, world, overlay;
        readonly List<BuildSpot> spots = new List<BuildSpot>();
        Pool<Enemy> enemies;
        Pool<Projectile> projectiles;
        Pool<Effect> effects;
        SpriteRenderer rangeDecal, padMarker;
        float selectionTime;

        // Spawning state of the current wave.
        readonly List<SpawnGroup> groups = new List<SpawnGroup>();
        readonly List<int> groupRemaining = new List<int>();
        readonly List<float> groupTimer = new List<float>();
        int stressSpawnCycle;

        public void Init(Camera camera)
        {
            Instance = this;
            cam = camera;
            flashBlock = new MaterialPropertyBlock();
            FitCamera();
            billboard = cam.transform.rotation;
            Balance = Corehold.Balance.Load();

            // 3D models live under the world root; billboards (health bars, effects) live under the overlay.
            world = new GameObject("World").transform;
            overlay = new GameObject("Overlay").transform;
            CreatePools();

            rangeDecal = FlatSprite("RangeDecal", Art.RangeDecal, new Color(0.6f, 0.88f, 1f, 0.9f));
            padMarker = FlatSprite("PadMarker", Art.PadMarker, new Color(1f, 0.85f, 0.35f, 0.95f));
            ShowRange(Vector2.zero, 0f);

            ShowMenu();
        }

        // ---------------- Level flow ----------------

        // The menu shows the vehicle garage as a live backdrop.
        public void ShowMenu()
        {
            ClearLevel();
            Time.timeScale = 1f;
            levelRoot = GarageView.Build(world, cam).transform;
            Sfx.Music(Sounds.MenuMusic, 0.5f);
            State = GameState.Menu;
            StateChanged?.Invoke();
        }

        public void LoadLevel(int index)
        {
            ClearLevel();
            LevelIndex = Mathf.Clamp(index, 0, Balance.levels.Length - 1);
            Level = Balance.levels[LevelIndex];

            var view = FitCamera();
            path = new LevelPath(ExtendStartOffscreen(Level.path, view));
            levelRoot = LevelView.Build(Level, path, Balance.economy.roadWidth, view, 1000 + LevelIndex * 97, world, spots);

            Coins = Level.startCoins;
            Lives = Level.lives;
            Kills = 0;
            WavesStarted = 0;
            Spawning = false;
            NextWaveTimer = Balance.economy.firstWaveDelay;
            Sfx.Music(Sounds.BattleMusic, 0.32f);
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

        // Frames the play area for the current screen; returns the visible map area.
        Rect FitCamera()
        {
            CameraRig.Fit(cam);
            camHome = cam.transform.position;
            shake = 0f;
            zoom = 1f;
            pan = Vector3.zero;
            var forward = cam.transform.forward;
            groundDistance = -camHome.y / forward.y;
            var view = CameraRig.VisibleMap(cam);
            visibleSize = new Vector2(view.width, view.height * View.ZStretch);
            return view;
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
            Sfx.Play(Sounds.WaveHorn, 0.55f, 0f, 0f);
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
            else if (WavesStarted < WaveCount && !StressMode)
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

            SetupParts(e, type.id == "tank" ? Tank : type.air ? Drone : Soldier);
            e.root.localScale = Vector3.one * (type.size * UnitScale);
            e.root.localPosition = View.World(e.position, e.height);
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

                float bob = Animate(e, time);
                e.root.localPosition = View.World(e.position, e.height + bob);
                e.root.localRotation = View.Yaw(e.angle);
                if (e.flash > 0f) SetFlash(e, Mathf.Max(0f, e.flash - FlashFade * dt));
                if (e.hpRoot.gameObject.activeSelf) e.hpRoot.position = View.World(e.position, e.height + e.type.size * 1.3f * UnitScale + 0.2f);
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
            Flash(e.position, 0.8f, 1.6f, Art.Palette.Danger);
            Smoke(e.position, 0.6f, 3, 1f);
            shake = Mathf.Max(shake, 0.12f);
            Sfx.Play(Sounds.Alarm, 0.5f, 0.4f, 0f);
            ReleaseEnemy(e);
        }

        void Damage(Enemy e, float amount)
        {
            e.health -= amount;
            if (e.health > 0f)
            {
                e.flash = 1f;
                SetHealthBar(e);
                return;
            }
            Kills++;
            Coins += e.type.reward;
            Explosion(e.position, e.height + 0.3f, e.type.size);
            if (e.type.id == "tank") shake = Mathf.Max(shake, 0.08f);
            ReleaseEnemy(e);
        }

        void SetFlash(Enemy e, float value)
        {
            e.flash = value;
            flashBlock.SetFloat(FlashId, value);
            foreach (var r in e.renderers)
                if (r.gameObject.activeSelf) r.SetPropertyBlock(flashBlock);
        }

        // Puts the right meshes on a pooled enemy's body and parts (only when its unit kind changes).
        static void SetupParts(Enemy e, int kind)
        {
            if (e.kind == kind) return;
            e.kind = kind;
            e.body.sharedMesh = kind == Tank ? Models.Tank : kind == Drone ? Models.Drone : Models.Soldier;
            int used = kind == Soldier ? 2 : kind == Tank ? 1 : 4;
            for (int i = 0; i < Enemy.PartCount; i++)
            {
                var part = e.parts[i];
                part.gameObject.SetActive(i < used);
                if (i >= used) continue;
                part.localRotation = Quaternion.identity;
                if (kind == Soldier)
                {
                    e.partFilters[i].sharedMesh = Models.SoldierLeg;
                    part.localPosition = new Vector3(0f, Models.SoldierHip, i == 0 ? Models.SoldierLegOffset : -Models.SoldierLegOffset);
                }
                else if (kind == Tank)
                {
                    e.partFilters[i].sharedMesh = Models.TankTurret;
                    part.localPosition = Models.TankTurretPivot;
                }
                else
                {
                    e.partFilters[i].sharedMesh = Models.DroneRotor;
                    float r = Models.DroneRotorReach;
                    part.localPosition = new Vector3(i < 2 ? r : -r, Models.DroneRotorHeight, i % 2 == 0 ? r : -r);
                }
            }
        }

        // Walk cycle, turret sway, spinning rotors. Returns the body's vertical bob.
        static float Animate(Enemy e, float time)
        {
            switch (e.kind)
            {
                case Soldier:
                {
                    float swing = Mathf.Sin(e.distance * 7f);
                    e.parts[0].localRotation = Quaternion.Euler(0f, 0f, swing * 35f);
                    e.parts[1].localRotation = Quaternion.Euler(0f, 0f, -swing * 35f);
                    return Mathf.Abs(swing) * 0.04f;
                }
                case Tank:
                    e.parts[0].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 0.7f + e.activeIndex) * 18f, 0f);
                    return Mathf.Sin(time * 40f + e.activeIndex) * 0.006f;      // engine rumble
                default:
                    for (int i = 0; i < 4; i++)
                        e.parts[i].localRotation = Quaternion.Euler(0f, time * (i % 2 == 0 ? 1500f : -1500f), 0f);
                    return Mathf.Sin(time * 3f + e.activeIndex) * 0.08f;
            }
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
            e.hpRoot.position = View.World(e.position, e.height + e.type.size * 1.3f * UnitScale + 0.2f);
        }

        void ReleaseEnemy(Enemy e)
        {
            if (e.flash > 0f) SetFlash(e, 0f);
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
                if (spot.tower != null) ShowRange(spot.position, Formulas.TowerRange(spot.tower.type, spot.tower.level));
                else ShowPadMarker(spot.position);
            }
            else ShowRange(Vector2.zero, 0f);
            SelectionChanged?.Invoke();
        }

        public void Select(int spotIndex) => Select(spots[spotIndex]);

        public void Deselect()
        {
            if (Selected == null && !rangeDecal.enabled && !padMarker.enabled) return;
            Select((BuildSpot)null);
        }

        // Range decal around a tower; range 0 hides it.
        public void ShowRange(Vector2 center, float range)
        {
            padMarker.enabled = false;
            rangeDecal.enabled = range > 0f;
            if (!rangeDecal.enabled) return;
            rangeDecal.transform.localPosition = View.World(center, 0.13f);
            rangeDecal.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
            selectionTime = 0f;
        }

        void ShowPadMarker(Vector2 center)
        {
            rangeDecal.enabled = false;
            padMarker.enabled = true;
            padMarker.transform.localPosition = View.World(center, 0.13f);
            selectionTime = 0f;
        }

        // The range decal turns slowly and pops in; the pad marker pulses.
        void UpdateSelection(float dt)
        {
            if (!rangeDecal.enabled && !padMarker.enabled) return;
            selectionTime += dt;
            float pop = 1f + 0.12f * Mathf.Exp(-selectionTime * 10f) * Mathf.Sin(selectionTime * 30f);
            if (rangeDecal.enabled && Selected?.tower != null)
            {
                rangeDecal.transform.localRotation = Quaternion.Euler(90f, 0f, selectionTime * 12f);
                var s = rangeDecal.transform.localScale;
                float r = Formulas.TowerRange(Selected.tower.type, Selected.tower.level) * 2f * pop;
                rangeDecal.transform.localScale = new Vector3(r, r, s.z);
            }
            if (padMarker.enabled)
            {
                float k = 1.35f * (1f + 0.06f * Mathf.Sin(selectionTime * 5f)) * pop;
                padMarker.transform.localScale = new Vector3(k, k, 1f);
                padMarker.transform.localRotation = Quaternion.Euler(90f, 0f, selectionTime * 25f);
            }
        }

        // A sprite lying flat on the ground.
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
            tower.root.transform.localPosition = View.World(spot.position, 0.1f);
            tower.baseFilter = MeshObject("Base", Models.TowerBase(0), tower.root.transform).GetComponent<MeshFilter>();
            tower.turret = MeshObject("Turret", Models.Turret(type.id, 0), tower.root.transform);
            tower.turretFilter = tower.turret.GetComponent<MeshFilter>();
            tower.bounce = 1f;
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
            Flash(spot.position, 0.4f, 1.4f, Art.Palette.Accent);
            Smoke(spot.position, 0.1f, 4, 0.8f);
            Sfx.Play(Sounds.Build, 0.8f);
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
            // The tower visibly grows: new base and turret models, and a little pop.
            t.baseFilter.sharedMesh = Models.TowerBase(t.level);
            t.turretFilter.sharedMesh = Models.Turret(t.type.id, t.level);
            t.bounce = 1f;
            Smoke(Selected.position, 0.1f, 3, 0.7f);
            Sfx.Play(Sounds.Upgrade, 0.7f, 0f, 0f);
            Flash(Selected.position, 0.6f, 1.3f, Art.Palette.Gold);
            Select(Selected);
            return true;
        }

        public bool TrySell()
        {
            if (State != GameState.Playing || Selected?.tower == null) return false;
            var spot = Selected;
            Coins += Formulas.SellValue(spot.tower.spent, Balance.economy);
            Destroy(spot.tower.root);
            Sfx.Play(Sounds.Sell, 0.7f, 0f, 0f);
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
                if (t.bounce > 0f)
                {
                    t.bounce = Mathf.Max(0f, t.bounce - dt / BounceTime);
                    float k = 1f - t.bounce;
                    t.root.transform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI * 2.5f) * 0.18f * t.bounce);
                }
                if (t.recoil > 0f)
                {
                    t.recoil = Mathf.Max(0f, t.recoil - RecoilRecovery * dt);
                    t.turret.localPosition = new Vector3(0f, TowerTop, 0f) - View.Yaw(t.angle) * Vector3.right * (RecoilDistance * t.recoil);
                }
                if (t.target == null)
                {
                    if (t.cooldown < 0f) t.cooldown = 0f;
                    t.angle += TurretScanSpeed * dt;
                    t.turret.localRotation = View.Yaw(t.angle);
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
            SpawnEffect(p.position, MuzzleHeight, 0.06f, MuzzleColor, t.type.splash > 0f ? 0.7f : 0.4f, 0.2f);
            if (t.type.splash > 0f) Smoke(p.position, MuzzleHeight, 1, 0.5f);
            if (t.type.projectile == "shell") Sfx.Play(Sounds.Cannon, 0.6f, 0.08f);
            else if (t.type.projectile == "missile") Sfx.Play(Sounds.Missile, 0.45f, 0.08f);
            else Sfx.Play(Sounds.Mg, 0.3f, 0.05f, 0.1f);
            p.source = t;
            t.recoil = 1f;
            p.damage = Formulas.TowerDamage(t.type, t.level);
            p.target = target;
            p.targetGeneration = target.generation;
            p.aim = target.position;

            p.filter.sharedMesh = t.type.projectile == "shell" ? Models.Shell
                : t.type.projectile == "missile" ? Models.Missile : Models.Bullet;
            p.transform.localPosition = View.World(p.position, p.height);
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
                p.transform.localPosition = View.World(p.position, p.height);
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
                Explosion(at, 0.2f, type.splash);
                return;
            }
            if (target == null) return;
            if (type.projectile == "missile") Explosion(at, height, 0.45f);
            else
            {
                SpawnEffect(at, height, 0.08f, SparkColor, 0.35f, 0.15f);
                Sfx.Play(Sounds.Hit, 0.18f, 0.06f, 0.15f);
            }
            Damage(target, damage);
        }

        // ---------------- Effects ----------------

        Effect SpawnEffect(Vector2 at, float height, float life, Color color, float startSize, float endSize)
        {
            var fx = effects.Get();
            fx.age = 0f;
            fx.life = life;
            fx.startSize = startSize;
            fx.endSize = endSize;
            fx.color = color;
            fx.velocity = Vector3.zero;
            fx.transform.position = View.World(at, height);
            fx.transform.localScale = Vector3.one * startSize;
            fx.renderer.color = color;
            return fx;
        }

        // Bright expanding flash.
        void Flash(Vector2 at, float height, float size, Color color) =>
            SpawnEffect(at, height, 0.3f, color, size * 0.4f, size);

        // Puffs of grey smoke drifting up and out.
        void Smoke(Vector2 at, float height, int puffs, float size)
        {
            for (int i = 0; i < puffs; i++)
            {
                var fx = SpawnEffect(at + UnityEngine.Random.insideUnitCircle * 0.2f * size, height,
                    UnityEngine.Random.Range(0.7f, 1.1f), SmokeColor, size * 0.5f, size * UnityEngine.Random.Range(1.1f, 1.5f));
                var drift = UnityEngine.Random.insideUnitCircle * 0.5f;
                fx.velocity = new Vector3(drift.x, UnityEngine.Random.Range(0.6f, 1.1f), drift.y);
            }
        }

        // Fireball, a white core and smoke; size ~ blast radius in map units.
        void Explosion(Vector2 at, float height, float size)
        {
            SpawnEffect(at, height, 0.3f, Art.Palette.Explosion, size * 0.8f, size * 2.4f);
            if (size >= 0.8f) Sfx.Play(Sounds.BigExplosion, 0.7f, 0.1f);
            else Sfx.Play(Sounds.Explosion, 0.45f, 0.07f);
            SpawnEffect(at, height + 0.05f, 0.15f, FireCoreColor, size * 0.6f, size * 1.2f);
            Smoke(at, height, 3, size * 1.4f);
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
                if (fx.velocity != Vector3.zero) fx.transform.position += fx.velocity * dt;
                fx.transform.localScale = Vector3.one * Mathf.Lerp(fx.startSize, fx.endSize, Mathf.Sqrt(t));
                var c = fx.color;
                c.a = fx.color.a * (1f - t * t);
                fx.renderer.color = c;
            }
        }

        // Zoomed and panned camera plus a short shake for big hits.
        void UpdateCamera(float dt)
        {
            shake = Mathf.Max(0f, shake - dt * 0.6f);
            ClampPan();
            var position = camHome + cam.transform.forward * (groundDistance * (1f - 1f / zoom)) + pan;
            if (shake > 0f) position += (Vector3)(UnityEngine.Random.insideUnitCircle * shake);
            cam.transform.position = position;
        }

        // Keep the zoomed view over the battlefield: the further in, the further it may pan.
        void ClampPan()
        {
            float free = 1f - 1f / zoom;
            float maxX = visibleSize.x * free * 0.5f, maxZ = visibleSize.y * free * 0.5f;
            pan = new Vector3(Mathf.Clamp(pan.x, -maxX, maxX), 0f, Mathf.Clamp(pan.z, -maxZ, maxZ));
        }

        // ---------------- Frame loop ----------------

        void Update()
        {
            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;

            HandleInput();
            UpdateWaves(dt);
            UpdateEnemies(dt);
            UpdateTowers(dt);
            UpdateProjectiles(dt);
            UpdateEffects(dt);
            UpdateSelection(Time.unscaledDeltaTime);
            UpdateCamera(Time.unscaledDeltaTime);

            if (Lives <= 0)
                Finish(GameState.Lost);
            else if (!StressMode && WavesStarted == WaveCount && !Spawning && enemies.Active.Count == 0)
                Finish(GameState.Won);
        }

        void Finish(GameState result)
        {
            Deselect();
            shake = 0f;
            Sfx.Music(null);
            Sfx.Play(result == GameState.Won ? Sounds.Victory : Sounds.Defeat, 0.8f, 0f, 0f);
            State = result;
            Time.timeScale = 1f;
            StateChanged?.Invoke();
        }

        // Two fingers pinch to zoom; one finger drags to pan when zoomed in; a short press without moving is a tap.
        void HandleInput()
        {
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f) zoom = Mathf.Clamp(zoom * (1f + wheel * 0.1f), 1f, MaxZoom);

            if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0).position;
                var b = Input.GetTouch(1).position;
                float d = Mathf.Max((a - b).magnitude, 1f);
                if (!pinching)
                {
                    pinching = true;
                    pinchStartDistance = d;
                    pinchStartZoom = zoom;
                }
                zoom = Mathf.Clamp(pinchStartZoom * d / pinchStartDistance, 1f, MaxZoom);
                dragged = true;         // the gesture never ends in a tap
                return;
            }
            pinching = false;

            Vector2 pointer = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                pressing = !PointerOverUI();
                dragged = false;
                pressPosition = lastPointer = pointer;
            }
            else if (pressing && Input.GetMouseButton(0))
            {
                if ((pointer - pressPosition).magnitude > DragThreshold * Screen.height) dragged = true;
                if (dragged && zoom > 1.01f)
                {
                    // Move the world with the finger.
                    var delta = pointer - lastPointer;
                    pan.x -= delta.x * visibleSize.x / zoom / Screen.width;
                    pan.z -= delta.y * visibleSize.y / zoom / Screen.height;
                }
                lastPointer = pointer;
            }
            else if (pressing && Input.GetMouseButtonUp(0))
            {
                pressing = false;
                if (!dragged) Tap(pointer);
            }
        }

        void Tap(Vector2 screenPoint)
        {
            var ray = cam.ScreenPointToRay(screenPoint);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
            var hit = ray.GetPoint(enter);
            var p = new Vector2(hit.x, hit.z / View.ZStretch);

            BuildSpot picked = null;
            float best = LevelView.SpotPickRadius * LevelView.SpotPickRadius;
            foreach (var s in spots)
            {
                // Towers stand up from their pad, so also accept taps slightly above it on screen.
                var d = s.position - p;
                if (s.tower != null && d.y < 0f) d.y = Mathf.Min(0f, d.y + 0.5f);
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
                var root = new GameObject("Enemy").transform;
                root.SetParent(enemyRoot, false);
                var hp = new GameObject("HealthBar").transform;
                hp.SetParent(barRoot, false);
                hp.rotation = billboard;
                var body = MeshObject("Body", Models.Soldier, root);
                var e = new Enemy { root = root, body = body.GetComponent<MeshFilter>(), hpRoot = hp };
                e.renderers[0] = body.GetComponent<MeshRenderer>();
                for (int i = 0; i < Enemy.PartCount; i++)
                {
                    var part = MeshObject("Part", Models.SoldierLeg, root);
                    e.parts[i] = part;
                    e.partFilters[i] = part.GetComponent<MeshFilter>();
                    e.renderers[i + 1] = part.GetComponent<MeshRenderer>();
                }
                SetupParts(e, Soldier);
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
