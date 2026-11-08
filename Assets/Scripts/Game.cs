using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Corehold
{
    public enum GameState { Menu, Playing, Won, Lost }

    // Owns a level: path, build spots, towers, waves, enemies, projectiles, effects, coins and lives.
    // Logic runs in flat map coordinates; see View for how they map to the 3D world.
    public sealed class Game : MonoBehaviour
    {
        public const int StressEnemyCount = 200;
        const int EnemyPrewarm = 260;
        const int ProjectilePrewarm = 200;
        const int EffectPrewarm = 260;
        const int BoltPrewarm = 32;
        const int RipplePrewarm = 48;
        const float TurretTurnSpeed = 540f;
        const float EnemyTurnSpeed = 360f;
        const float HitRadius = 0.12f;
        const float HealthBarWidth = 0.6f;
        const float AirHeight = 1.3f;
        const float TowerTop = 0.32f;          // turret mount height on the tower base
        const float MuzzleHeight = 0.45f;
        const float UnitScale = 1.3f;          // models are drawn larger than their logic size for readability
        const float TurretScanSpeed = 20f;     // idle turrets slowly sweep
        const float RecoilDistance = 0.07f, RecoilRecovery = 7f;
        const float MaxZoom = 2.2f;
        const float DragThreshold = 0.025f;    // fraction of screen height a press may move and still count as a tap
        const float BounceTime = 0.45f;
        const float FlashFade = 8f;            // enemy hit flash fades in 1/8 s
        const float MortarArc = 2.2f;
        const float SpawnSpread = 0.35f;       // soldiers dropped by a destroyed APC spread along the road
        static readonly Color SmokeColor = new Color(0.42f, 0.42f, 0.4f, 0.55f);
        static readonly Color MuzzleColor = Art.Hex("FFE9A8"), SparkColor = Art.Hex("FFD27A"), FireCoreColor = Art.Hex("FFF4D6");
        static readonly Color HealColor = Art.Hex("5CF08A");
        static readonly Color FlameColor = Art.Hex("FF8A3D"), BoltColor = Art.Hex("9EE8FF"), TracerColor = Art.Hex("FFF1B0"), IceColor = Art.Hex("9EE8FF");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public static Game Instance { get; private set; }

        public BalanceData Balance { get; private set; }
        public GameState State { get; private set; }
        public int LevelIndex { get; private set; }
        public LevelConfig Level { get; private set; }
        public bool Endless { get; private set; }
        public int Coins { get; private set; }
        public int Lives { get; private set; }
        public bool Revived { get; private set; }   // the rewarded "continue" is offered once per attempt
        public int Kills { get; private set; }
        public int WavesStarted { get; private set; }
        public int WaveCount => Endless ? int.MaxValue : Level != null ? Level.waves.Length : 0;
        public bool Spawning { get; private set; }
        public float NextWaveTimer { get; private set; }
        public bool StressMode { get; set; }
        public BuildSpot Selected { get; private set; }
        public IReadOnlyList<BuildSpot> Spots => spots;

        // Saved campaign progress (see Progress).
        public int[] LevelStars => Progress.Stars(Balance.levels.Length);
        public int EndlessBest => Progress.EndlessBest;
        public bool IsLevelUnlocked(int index) => Progress.Unlocked(index);

        // Towers unlock as the campaign goes on; Endless has them all.
        public bool IsTowerUnlocked(string id) => Endless || Level == null || Level.number >= Balance.campaign.TowerLevel(id);
        public WorldMapView Map { get; private set; }        // the main menu's campaign map, null outside the menu

        public int EnemyCount => enemies.Active.Count;
        public float Zoom { get => zoom; set => zoom = Mathf.Clamp(value, 1f, MaxZoom); }
        public int ProjectileCount => projectiles.Active.Count;
        public int PoolGrowth => enemies.Created + projectiles.Created + effects.Created + bolts.Created + ripples.Created;
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
        Pool<Bolt> bolts;
        Pool<Ripple> ripples;
        SpriteRenderer rangeDecal, padMarker;
        float selectionTime;
        readonly List<Enemy> chainHits = new List<Enemy>();
        readonly Vector3[] boltPoints = new Vector3[6];

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
            Progress.Stars(Balance.levels.Length);

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

        // The menu is the campaign map with one marker per mission.
        public void ShowMenu()
        {
            ClearLevel();
            Time.timeScale = 1f;
            Map = WorldMapView.Build(world, cam, Balance, LevelStars);
            levelRoot = Map.transform;
            Sfx.Music(Sounds.MenuMusic, 0.5f);
            State = GameState.Menu;
            StateChanged?.Invoke();
        }

        public void LoadLevel(int index)
        {
            LevelIndex = Mathf.Clamp(index, 0, Balance.levels.Length - 1);
            Begin(Balance.levels[LevelIndex], false, 1000 + LevelIndex * 97);
        }

        public void StartEndless()
        {
            LevelIndex = -1;
            Begin(Balance.endless.ToLevel(), true, 4242);
        }

        void Begin(LevelConfig level, bool endless, int seed)
        {
            ClearLevel();
            Level = level;
            Endless = endless;
            peekIndex = -1;

            var view = FitCamera();
            var style = BiomeStyle.Of(Level.biome);
            cam.backgroundColor = style.Sky;
            RenderSettings.fogColor = style.Sky;
            path = new LevelPath(LevelPath.Rounded(ExtendStartOffscreen(Level.path, view)));
            levelRoot = LevelView.Build(Level, path, Balance.economy.roadWidth, view, seed, world, spots);

            Coins = Level.startCoins;
            Lives = Level.lives;
            Kills = 0;
            Revived = false;
            WavesStarted = 0;
            Spawning = false;
            NextWaveTimer = Balance.economy.firstWaveDelay;
            Sfx.Music(Sounds.BattleMusic, 0.32f);
            State = GameState.Playing;
            StateChanged?.Invoke();
            Analytics.LevelStart(AnalyticsName);
        }

        string AnalyticsName => Endless ? "endless" : "mission_" + Level.number;

        public bool CanRevive => State == GameState.Lost && !Revived && !StressMode;

        // Rewarded ad watched on the defeat screen: back into the same fight with a few lives.
        public void Revive()
        {
            if (!CanRevive) return;
            Revived = true;
            Lives = Balance.ads.reviveLives;
            Sfx.Music(Sounds.BattleMusic, 0.32f);
            State = GameState.Playing;
            StateChanged?.Invoke();
            Analytics.Event("revive", FirebaseLevelParam, AnalyticsName);
        }

        const string FirebaseLevelParam = "level_name";

        public void RestartLevel()
        {
            if (Endless) StartEndless();
            else LoadLevel(LevelIndex);
        }

        public void NextLevel()
        {
            if (LevelIndex + 1 < Balance.levels.Length) LoadLevel(LevelIndex + 1);
            else ShowMenu();
        }

        void ClearLevel()
        {
            Deselect();
            enemies.ReleaseAll();
            projectiles.ReleaseAll();
            effects.ReleaseAll();
            bolts.ReleaseAll();
            ripples.ReleaseAll();
            spots.Clear();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            Map = null;
            Level = null;
            Endless = false;
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
            groups.Clear();
            groupRemaining.Clear();
            groupTimer.Clear();
            var wave = PeekNextWave();
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

        // The upcoming wave's groups (for the wave preview); null when there is none.
        public WaveConfig PeekNextWave()
        {
            if (Level == null || WavesStarted >= WaveCount) return null;
            if (!Endless) return Level.waves[WavesStarted];
            if (peekIndex != WavesStarted)
            {
                peekIndex = WavesStarted;
                peekWave = EndlessWave(WavesStarted);
            }
            return peekWave;
        }

        WaveConfig peekWave;
        int peekIndex = -1;

        // Endless waves spend a growing threat budget on 1-3 unlocked enemy types; every bossEvery-th wave adds bosses.
        WaveConfig EndlessWave(int index)
        {
            var cfg = Balance.endless;
            var random = new System.Random(index * 7919 + 17);
            float budget = Formulas.EndlessBudget(cfg, index);
            var list = new List<SpawnGroup>();
            if ((index + 1) % cfg.bossEvery == 0)
            {
                var bosses = Array.FindAll(Balance.enemies, e => e.boss);
                var boss = bosses[(index / cfg.bossEvery) % bosses.Length];
                int count = 1 + index / (cfg.bossEvery * 2);
                list.Add(new SpawnGroup { enemy = boss.id, count = count, interval = 6f, delay = 4f });
                budget = Mathf.Max(budget * 0.4f, budget - boss.threat * count);
            }
            var unlocked = new List<string>();
            foreach (var u in cfg.unlocks) if (u.wave <= index) unlocked.Add(u.enemy);
            int kinds = Mathf.Min(unlocked.Count, 1 + random.Next(3));
            for (int k = 0; k < kinds; k++)
            {
                // Newer enemies are more likely, so each wave feels different.
                int pick = Mathf.Min(unlocked.Count - 1, (int)(Mathf.Sqrt((float)random.NextDouble()) * unlocked.Count));
                var type = Balance.Enemy(unlocked[pick]);
                unlocked.RemoveAt(pick);
                int count = Mathf.Max(1, Mathf.RoundToInt(budget / kinds / type.threat));
                float interval = Mathf.Clamp(cfg.spawnInterval * Mathf.Sqrt(type.threat), 0.3f, 3f);
                list.Add(new SpawnGroup { enemy = type.id, count = count, interval = interval, delay = k * 2.5f });
            }
            return new WaveConfig { groups = list.ToArray() };
        }

        void UpdateWaves(float dt)
        {
            if (StressMode)
            {
                var types = Balance.enemies;
                while (enemies.Active.Count < StressEnemyCount)
                {
                    var type = types[stressSpawnCycle++ % types.Length];
                    if (!type.boss) SpawnEnemy(type, Mathf.Max(0, WavesStarted - 1), UnityEngine.Random.value * path.Length * 0.9f);
                }
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
                        SpawnEnemy(type, WavesStarted - 1, 0f);
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

        Enemy SpawnEnemy(EnemyConfig type, int waveIndex, float distance)
        {
            var e = enemies.Get();
            e.type = type;
            // Bosses scale with the mission, not with the wave they come in.
            if (type.boss) waveIndex = 0;
            e.maxHealth = e.health = Endless
                ? Formulas.EndlessHealth(type, Balance.endless, waveIndex)
                : Formulas.EnemyHealth(type, Level, Balance.waves, waveIndex);
            e.distance = Mathf.Clamp(distance, 0f, path.Length * 0.98f);
            e.segment = 0;
            e.position = path.Sample(e.distance, ref e.segment, out var dir);
            e.angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            e.height = type.air ? AirHeight : 0f;
            e.slow = e.slowTime = e.burnDps = e.burnTime = 0f;
            e.abilityTimer = 1f;

            SetupParts(e, Models.Rig(type.id));
            e.root.localScale = Vector3.one * (type.size * UnitScale);
            e.root.localPosition = View.World(e.position, e.height);
            e.root.localRotation = View.Yaw(e.angle);
            float barWidth = type.boss ? 2.5f : 1f;
            e.hpBackRenderer.transform.localScale = new Vector3(HealthBarWidth * barWidth + 0.05f, type.boss ? 0.18f : 0.13f, 1f);
            SetHealthBar(e);
            return e;
        }

        void UpdateEnemies(float dt)
        {
            float time = Time.time;
            var list = enemies.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                float speed = e.type.speed;
                if (e.slowTime > 0f)
                {
                    e.slowTime -= dt;
                    speed *= 1f - e.slow;
                }
                if (e.burnTime > 0f)
                {
                    e.burnTime -= dt;
                    e.burnFx -= dt;
                    if (e.burnFx <= 0f)
                    {
                        e.burnFx = 0.25f;
                        var fx = SpawnEffect(e.position, e.height + 0.35f, 0.4f, FlameColor, 0.25f, 0.45f);
                        fx.velocity = new Vector3(0f, 0.8f, 0f);
                    }
                    if (Damage(e, e.burnDps * dt, false, true)) continue;
                }

                if (e.type.healRate > 0f || e.type.launchEvery > 0f)
                {
                    e.abilityTimer -= dt;
                    if (e.abilityTimer <= 0f) UseAbility(e);
                }

                e.distance += speed * dt;
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
                if (e.hpRoot.gameObject.activeSelf) e.hpRoot.position = HealthBarPosition(e);
            }
        }

        // Medics heal everyone around them once a second; carriers launch drones.
        void UseAbility(Enemy e)
        {
            var type = e.type;
            if (type.healRate > 0f)
            {
                e.abilityTimer = 1f;
                float r2 = type.healRadius * type.healRadius;
                var list = enemies.Active;
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    if (o.health >= o.maxHealth || (o.position - e.position).sqrMagnitude > r2) continue;
                    o.health = Mathf.Min(o.maxHealth, o.health + o.maxHealth * type.healRate);
                    SetHealthBar(o);
                }
                SpawnRipple(e.position, type.healRadius, HealColor);
            }
            else if (!StressMode)
            {
                e.abilityTimer = type.launchEvery;
                var drone = SpawnEnemy(Balance.Enemy(type.launch), Mathf.Max(0, WavesStarted - 1), e.distance - 0.3f);
                Flash(drone.position, drone.height, 0.8f, MuzzleColor);
            }
            else e.abilityTimer = type.launchEvery;
        }

        Vector3 HealthBarPosition(Enemy e) => View.World(e.position, e.height + e.type.size * 1.3f * UnitScale + 0.2f);

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
            shake = Mathf.Max(shake, e.type.boss ? 0.3f : 0.12f);
            Sfx.Play(Sounds.Alarm, 0.5f, 0.4f, 0f);
            ReleaseEnemy(e);
        }

        // Returns true when the enemy died (and was released). Armour soaks part of every hit except burning.
        bool Damage(Enemy e, float amount, bool flash = true, bool ignoreArmor = false)
        {
            if (!ignoreArmor && e.type.armor > 0f) amount = Formulas.AfterArmor(amount, e.type.armor);
            e.health -= amount;
            if (e.health > 0f)
            {
                if (flash) e.flash = 1f;
                SetHealthBar(e);
                return false;
            }
            Kills++;
            Coins += e.type.reward;
            Explosion(e.position, e.height + 0.3f, e.type.size);
            if (e.type.size >= 0.85f) shake = Mathf.Max(shake, e.type.boss ? 0.35f : 0.08f);
            var drop = e.type.spawnOnDeath;
            int dropCount = e.type.spawnCount;
            float at = e.distance;
            ReleaseEnemy(e);
            if (!string.IsNullOrEmpty(drop) && !StressMode)
            {
                var type = Balance.Enemy(drop);
                for (int i = 0; i < dropCount; i++)
                    SpawnEnemy(type, Mathf.Max(0, WavesStarted - 1), at - i * SpawnSpread);
            }
            return true;
        }

        void SetFlash(Enemy e, float value)
        {
            e.flash = value;
            flashBlock.SetFloat(FlashId, value);
            foreach (var r in e.renderers)
                if (r.gameObject.activeSelf) r.SetPropertyBlock(flashBlock);
        }

        // Puts the right meshes on a pooled enemy's body and parts (only when its model changes).
        static void SetupParts(Enemy e, Models.UnitRig rig)
        {
            if (e.rig == rig) return;
            e.rig = rig;
            e.body.sharedMesh = rig.Body;
            for (int i = 0; i < Enemy.PartCount; i++)
            {
                var part = e.parts[i];
                bool used = i < rig.Parts.Length;
                part.gameObject.SetActive(used);
                if (!used) continue;
                e.partFilters[i].sharedMesh = rig.Parts[i];
                part.localPosition = rig.Joints[i];
                part.localRotation = Quaternion.identity;
            }
        }

        // Walk cycle, turret sway, wheels and rotors. Returns the body's vertical bob.
        static float Animate(Enemy e, float time)
        {
            switch (e.rig.Motion)
            {
                case Models.Motion.Walk:
                {
                    float swing = Mathf.Sin(e.distance * 7f);
                    e.parts[0].localRotation = Quaternion.Euler(0f, 0f, swing * 35f);
                    e.parts[1].localRotation = Quaternion.Euler(0f, 0f, -swing * 35f);
                    return Mathf.Abs(swing) * 0.04f;
                }
                case Models.Motion.Turret:
                    for (int i = 0; i < e.rig.Parts.Length; i++)
                        e.parts[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * (0.7f + i * 0.4f) + e.activeIndex + i) * (18f + i * 15f), 0f);
                    return Mathf.Sin(time * 40f + e.activeIndex) * 0.006f;      // engine rumble
                case Models.Motion.Fan:
                    e.parts[0].localRotation = Quaternion.Euler(time * 1400f, 0f, 0f);
                    return 0.08f + Mathf.Sin(time * 6f + e.activeIndex) * 0.02f;     // floats on its air cushion
                case Models.Motion.None:
                    return Mathf.Sin(time * 2f + e.activeIndex) * 0.05f;
                case Models.Motion.Tandem:
                    e.parts[0].localRotation = Quaternion.Euler(0f, time * 900f, 0f);
                    e.parts[1].localRotation = Quaternion.Euler(0f, -time * 900f, 0f);
                    return Mathf.Sin(time * 1.2f + e.activeIndex) * 0.08f;
                case Models.Motion.Wheels:
                {
                    var spin = Quaternion.Euler(0f, 0f, -e.distance * 320f / Mathf.Max(e.type.size, 0.1f));
                    for (int i = 0; i < e.rig.Parts.Length; i++) e.parts[i].localRotation = spin;
                    return Mathf.Sin(time * 25f + e.activeIndex) * 0.008f;
                }
                case Models.Motion.Heli:
                    e.parts[0].localRotation = Quaternion.Euler(0f, time * 1300f, 0f);
                    e.parts[1].localRotation = Quaternion.Euler(0f, 0f, time * 2000f);
                    return Mathf.Sin(time * 1.5f + e.activeIndex) * 0.06f;
                default:
                    for (int i = 0; i < 4; i++)
                        e.parts[i].localRotation = Quaternion.Euler(0f, time * (i % 2 == 0 ? 1500f : -1500f), 0f);
                    return Mathf.Sin(time * 3f + e.activeIndex) * 0.08f;
            }
        }

        void SetHealthBar(Enemy e)
        {
            float frac = Mathf.Clamp01(e.health / e.maxHealth);
            bool show = frac < 0.999f || e.type.boss;
            if (e.hpRoot.gameObject.activeSelf != show) e.hpRoot.gameObject.SetActive(show);
            if (!show) return;
            float full = HealthBarWidth * (e.type.boss ? 2.5f : 1f);
            float w = full * frac;
            e.hpFill.localScale = new Vector3(w, e.type.boss ? 0.12f : 0.08f, 1f);
            e.hpFill.localPosition = new Vector3(-full * 0.5f + w * 0.5f, 0f, -0.01f);
            e.hpFillRenderer.color = e.type.boss ? Art.Palette.Danger
                : frac > 0.5f ? Art.Palette.Good : frac > 0.25f ? Art.Palette.Gold : Art.Palette.Danger;
            e.hpRoot.position = HealthBarPosition(e);
        }

        void ReleaseEnemy(Enemy e)
        {
            if (e.flash > 0f) SetFlash(e, 0f);
            e.generation++;
            enemies.Release(e);
        }

        static bool CanHit(TowerConfig t, Enemy e) => e.type.air ? t.hitsAir : t.hitsGround;

        // ---------------- Selection ----------------

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
                float r = Formulas.TowerRange(Selected.tower.type, Selected.tower.level) * 2f * pop;
                rangeDecal.transform.localScale = new Vector3(r, r, 1f);
            }
            if (padMarker.enabled)
            {
                float k = 1.35f * (1f + 0.06f * Mathf.Sin(selectionTime * 5f)) * pop;
                padMarker.transform.localScale = new Vector3(k, k, 1f);
                padMarker.transform.localRotation = Quaternion.Euler(90f, 0f, selectionTime * 25f);
            }
        }

        // A sprite lying flat on the ground.
        SpriteRenderer FlatSprite(string name, Sprite sprite, Color color, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : world, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = Layers.Range;
            return r;
        }

        // ---------------- Building ----------------

        public bool CanBuild(string towerId) =>
            State == GameState.Playing && Selected != null && Selected.tower == null && IsTowerUnlocked(towerId) &&
            Coins >= Balance.Tower(towerId).cost;

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

        // ---------------- Towers ----------------

        void UpdateTowers(float dt)
        {
            for (int s = 0; s < spots.Count; s++)
            {
                var t = spots[s].tower;
                if (t == null) continue;
                var pos = spots[s].position;
                var type = t.type;
                float range = Formulas.TowerRange(type, t.level);

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

                if (type.attack == "pulse")
                {
                    // Cryo: no aiming, the emitter spins and pulses whenever anything is in reach.
                    t.angle += 90f * dt;
                    t.turret.localRotation = View.Yaw(t.angle);
                    if (t.cooldown <= 0f && AnyInRange(type, pos, range))
                    {
                        Pulse(t, pos, range);
                        t.cooldown = 1f / Formulas.TowerFireRate(type, t.level);
                    }
                    else if (t.cooldown < 0f) t.cooldown = 0f;
                    continue;
                }

                if (!IsValidTarget(t, pos, range))
                {
                    t.target = FindTarget(type, pos, range);
                    t.targetGeneration = t.target != null ? t.target.generation : 0;
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

                float interval = 1f / Formulas.TowerFireRate(type, t.level);
                for (int shots = 0; t.cooldown <= 0f && shots < 4; shots++)
                {
                    if (t.target.generation != t.targetGeneration) break;
                    if (type.attack == "chain") Chain(t, pos, t.target);
                    else if (type.attack == "instant") Snipe(t, pos, t.target);
                    else Fire(t, pos, t.target);
                    t.cooldown += interval;
                }
            }
        }

        bool IsValidTarget(Tower t, Vector2 pos, float range)
        {
            var e = t.target;
            if (e == null || e.generation != t.targetGeneration) return false;
            float d2 = (e.position - pos).sqrMagnitude;
            // Snipers keep re-checking for a stronger target; others stick to theirs while it is in reach.
            return d2 <= range * range && d2 >= t.type.minRange * t.type.minRange && t.type.target != "strongest";
        }

        // Default: the enemy furthest along the path (closest to the base). "strongest": most health left.
        Enemy FindTarget(TowerConfig type, Vector2 pos, float range)
        {
            float r2 = range * range, min2 = type.minRange * type.minRange;
            bool strongest = type.target == "strongest";
            Enemy best = null;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!CanHit(type, e)) continue;
                float d2 = (e.position - pos).sqrMagnitude;
                if (d2 > r2 || d2 < min2) continue;
                if (best == null || (strongest ? e.health > best.health : e.distance > best.distance)) best = e;
            }
            return best;
        }

        bool AnyInRange(TowerConfig type, Vector2 pos, float range)
        {
            float r2 = range * range;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
                if (CanHit(type, list[i]) && (list[i].position - pos).sqrMagnitude <= r2) return true;
            return false;
        }

        Vector2 Muzzle(Tower t, Vector2 pos)
        {
            float rad = t.angle * Mathf.Deg2Rad;
            return pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 0.45f;
        }

        void Fire(Tower t, Vector2 pos, Enemy target)
        {
            var type = t.type;
            var p = projectiles.Get();
            p.position = p.start = Muzzle(t, pos);
            p.height = p.startHeight = MuzzleHeight;
            p.arc = type.projectile == "mortar";
            p.source = t;
            t.recoil = 1f;
            p.damage = Formulas.TowerDamage(type, t.level);
            p.target = target;
            p.targetGeneration = target.generation;
            p.aim = target.position;

            switch (type.projectile)
            {
                case "shell":
                    Sfx.Play(Sounds.Cannon, 0.6f, 0.08f);
                    break;
                case "missile":
                    Sfx.Play(Sounds.Missile, 0.45f, 0.08f);
                    break;
                case "mortar":
                    Sfx.Play(Sounds.Mortar, 0.6f, 0.1f);
                    // Lead the target a little: shells are slow.
                    int seg = target.segment;
                    p.aim = path.Sample(target.distance + target.type.speed * 0.9f, ref seg, out _);
                    break;
                case "flame":
                    Sfx.Play(Sounds.Flame, 0.35f, 0.12f);
                    break;
                default:
                    Sfx.Play(Sounds.Mg, 0.3f, 0.05f, 0.1f);
                    break;
            }
            if (type.projectile != "flame") SpawnEffect(p.position, MuzzleHeight, 0.06f, MuzzleColor, type.splash > 0f ? 0.7f : 0.4f, 0.2f);
            if (type.splash > 0f && type.projectile != "flame") Smoke(p.position, MuzzleHeight, 1, 0.5f);

            p.filter.sharedMesh = Models.Projectile(type.projectile);
            p.transform.localPosition = View.World(p.position, p.height);
            p.transform.localRotation = View.Yaw(t.angle);
        }

        // Tesla: instant hit that jumps to the nearest enemies not yet struck.
        void Chain(Tower t, Vector2 pos, Enemy first)
        {
            var type = t.type;
            float damage = Formulas.TowerDamage(type, t.level);
            int jumps = Formulas.ChainTargets(type, t.level);
            chainHits.Clear();
            var from = View.World(pos, 0.95f);
            var current = first;
            for (int n = 0; n <= jumps && current != null; n++)
            {
                chainHits.Add(current);
                var to = View.World(current.position, current.height + 0.3f);
                SpawnBolt(from, to, BoltColor, 0.14f, 0.07f, true);
                SpawnEffect(current.position, current.height + 0.3f, 0.15f, BoltColor, 0.3f, 0.6f);
                from = to;
                var hit = current;
                current = NearestUnhit(type, hit.position, type.chainRange);
                Damage(hit, damage * (n == 0 ? 1f : 0.75f));
            }
            Sfx.Play(Sounds.Zap, 0.45f, 0.08f);
        }

        Enemy NearestUnhit(TowerConfig type, Vector2 from, float range)
        {
            float best = range * range;
            Enemy pick = null;
            var list = enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!CanHit(type, e) || chainHits.Contains(e)) continue;
                float d2 = (e.position - from).sqrMagnitude;
                if (d2 < best) { best = d2; pick = e; }
            }
            return pick;
        }

        // Sniper: hitscan with a bright tracer.
        void Snipe(Tower t, Vector2 pos, Enemy target)
        {
            var muzzle = Muzzle(t, pos);
            t.recoil = 1f;
            SpawnBolt(View.World(muzzle, MuzzleHeight), View.World(target.position, target.height + 0.3f), TracerColor, 0.1f, 0.035f, false);
            SpawnEffect(muzzle, MuzzleHeight, 0.08f, MuzzleColor, 0.6f, 0.2f);
            SpawnEffect(target.position, target.height + 0.3f, 0.12f, SparkColor, 0.5f, 0.2f);
            Sfx.Play(Sounds.Sniper, 0.5f, 0.05f);
            Damage(target, Formulas.TowerDamage(t.type, t.level));
        }

        // Cryo: damages and slows everything in reach, with an expanding frost ring.
        void Pulse(Tower t, Vector2 pos, float range)
        {
            var type = t.type;
            float damage = Formulas.TowerDamage(type, t.level), slow = Formulas.Slow(type, t.level);
            float r2 = range * range;
            var list = enemies.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (!CanHit(type, e) || (e.position - pos).sqrMagnitude > r2) continue;
                e.slow = Mathf.Max(e.slowTime > 0f ? e.slow : 0f, slow);
                e.slowTime = type.slowTime;
                Damage(e, damage, false);
            }
            SpawnRipple(pos, range, IceColor);
            Sfx.Play(Sounds.Freeze, 0.35f, 0.15f);
        }

        void UpdateProjectiles(float dt)
        {
            var list = projectiles.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                var type = p.source.type;
                bool targetAlive = p.target.generation == p.targetGeneration;
                if (p.arc)
                {
                    // Mortar: fixed landing point, parabolic height.
                    var span = p.aim - p.start;
                    float total = Mathf.Max(span.magnitude, 0.01f);
                    float travelled = (p.position - p.start).magnitude + type.projectileSpeed * dt;
                    float k = Mathf.Clamp01(travelled / total);
                    var prev = View.World(p.position, p.height);
                    p.position = p.start + span * k;
                    p.height = Mathf.Lerp(p.startHeight, 0.1f, k) + Mathf.Sin(k * Mathf.PI) * MortarArc;
                    var now = View.World(p.position, p.height);
                    p.transform.localPosition = now;
                    if (now != prev) p.transform.localRotation = Quaternion.LookRotation(now - prev) * Quaternion.Euler(0f, -90f, 0f);
                    if (k >= 1f)
                    {
                        projectiles.Release(p);
                        Impact(p.source, p.aim, 0.1f, p.damage, null);
                    }
                    continue;
                }

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
                    Impact(p.source, p.aim, aimHeight, p.damage, targetAlive ? p.target : null);
                    continue;
                }
                // Climb or drop toward the target height over the remaining distance.
                p.height = Mathf.Lerp(p.height, aimHeight, step / dist);
                p.position += to * (step / dist);
                p.transform.localPosition = View.World(p.position, p.height);
                p.transform.localRotation = View.Yaw(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            }
        }

        void Impact(Tower source, Vector2 at, float height, float damage, Enemy target)
        {
            var type = source.type;
            if (type.splash > 0f)
            {
                bool flame = type.projectile == "flame";
                float r2 = type.splash * type.splash;
                float burn = Formulas.Burn(type, source.level);
                var list = enemies.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var e = list[i];
                    if (!CanHit(type, e) || (e.position - at).sqrMagnitude > r2) continue;
                    if (flame)
                    {
                        e.burnDps = Mathf.Max(e.burnTime > 0f ? e.burnDps : 0f, burn);
                        e.burnTime = type.burnTime;
                    }
                    Damage(e, damage, !flame);
                }
                if (flame) SpawnEffect(at, 0.3f, 0.3f, FlameColor, 0.4f, type.splash * 2f);
                else Explosion(at, 0.2f, type.splash);
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

        // Jagged (lightning) or straight (tracer) line that fades out.
        void SpawnBolt(Vector3 from, Vector3 to, Color color, float life, float width, bool jagged)
        {
            var b = bolts.Get();
            b.age = 0f;
            b.life = life;
            b.color = color;
            int n = jagged ? boltPoints.Length : 2;
            b.line.positionCount = n;
            var side = Vector3.Cross((to - from).normalized, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                var p = Vector3.Lerp(from, to, k);
                if (jagged && i > 0 && i < n - 1)
                    p += side * UnityEngine.Random.Range(-0.18f, 0.18f) + Vector3.up * UnityEngine.Random.Range(-0.12f, 0.12f);
                boltPoints[i] = p;
            }
            b.line.SetPositions(boltPoints);
            b.line.widthMultiplier = width;
            b.line.startColor = b.line.endColor = color;
        }

        void SpawnRipple(Vector2 at, float radius, Color color)
        {
            var r = ripples.Get();
            r.age = 0f;
            r.life = 0.5f;
            r.radius = radius;
            r.color = color;
            r.renderer.transform.localPosition = View.World(at, 0.15f);
            r.renderer.transform.localScale = Vector3.one * 0.2f;
            r.renderer.color = color;
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

            var bl = bolts.Active;
            for (int i = bl.Count - 1; i >= 0; i--)
            {
                var b = bl[i];
                b.age += dt;
                if (b.age >= b.life)
                {
                    bolts.Release(b);
                    continue;
                }
                var c = b.color;
                c.a = 1f - b.age / b.life;
                b.line.startColor = b.line.endColor = c;
            }

            var rl = ripples.Active;
            for (int i = rl.Count - 1; i >= 0; i--)
            {
                var r = rl[i];
                r.age += dt;
                float t = r.age / r.life;
                if (t >= 1f)
                {
                    ripples.Release(r);
                    continue;
                }
                float size = Mathf.Lerp(0.2f, r.radius * 2f, Mathf.Sqrt(t));
                r.renderer.transform.localScale = new Vector3(size, size, 1f);
                var c = r.color;
                c.a = 0.9f * (1f - t);
                r.renderer.color = c;
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
            else if (!Endless && !StressMode && WavesStarted == WaveCount && !Spawning && enemies.Active.Count == 0)
                Finish(GameState.Won);
        }

        // Stars for a won level: 3 with every life kept, 2 with at least half, otherwise 1.
        public int StarsFor(int lives, int maxLives) => lives >= maxLives ? 3 : lives * 2 >= maxLives ? 2 : 1;

        void Finish(GameState result)
        {
            Deselect();
            shake = 0f;
            if (Endless) Progress.EndlessBest = Mathf.Max(0, WavesStarted - 1);
            else if (result == GameState.Won) Progress.SetStars(LevelIndex, StarsFor(Lives, Level.lives));
            Sfx.Music(null);
            Sfx.Play(result == GameState.Won ? Sounds.Victory : Sounds.Defeat, 0.8f, 0f, 0f);
            State = result;
            Time.timeScale = 1f;
            Ads.OnResult();
            Analytics.LevelEnd(AnalyticsName, result == GameState.Won,
                result == GameState.Won && !Endless ? StarsFor(Lives, Level.lives) : 0, WavesStarted, Kills);
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
            var soldier = Models.Rig("soldier");
            enemies = new Pool<Enemy>(EnemyPrewarm, () =>
            {
                var root = new GameObject("Enemy").transform;
                root.SetParent(enemyRoot, false);
                var hp = new GameObject("HealthBar").transform;
                hp.SetParent(barRoot, false);
                hp.rotation = billboard;
                var body = MeshObject("Body", soldier.Body, root);
                var e = new Enemy { root = root, body = body.GetComponent<MeshFilter>(), hpRoot = hp };
                e.renderers[0] = body.GetComponent<MeshRenderer>();
                for (int i = 0; i < Enemy.PartCount; i++)
                {
                    var part = MeshObject("Part", soldier.Parts[0], root);
                    e.parts[i] = part;
                    e.partFilters[i] = part.GetComponent<MeshFilter>();
                    e.renderers[i + 1] = part.GetComponent<MeshRenderer>();
                }
                SetupParts(e, soldier);
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

            var lineMaterial = new Material(Shader.Find("Sprites/Default"));
            bolts = new Pool<Bolt>(BoltPrewarm, () =>
            {
                var go = new GameObject("Bolt");
                go.transform.SetParent(effectRoot, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sortingOrder = Layers.Effect + 1;
                return new Bolt { line = line };
            },
            (b, on) => b.line.gameObject.SetActive(on),
            b => b.activeIndex, (b, i) => b.activeIndex = i);

            ripples = new Pool<Ripple>(RipplePrewarm, () =>
                new Ripple { renderer = FlatSprite("Ripple", Art.Ring, Color.white, effectRoot) },
            (r, on) => r.renderer.gameObject.SetActive(on),
            r => r.activeIndex, (r, i) => r.activeIndex = i);
        }
    }
}
