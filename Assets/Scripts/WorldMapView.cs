using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Main menu: a 3D campaign map. A dirt road winds through the four zones (grass, desert, snow, autumn) past one marker
    // per mission; flags show won (gold), open (blue) and locked (grey) missions, and boss missions show their boss.
    // A convoy drives the road near the view, a helicopter crosses the sky, and the camera scrolls with a finger drag.
    // The terrain is built once per launch in chunks (culled when off screen); GameUI puts tappable buttons on the markers.
    public sealed class WorldMapView : MonoBehaviour
    {
        public const float Spacing = 4.4f;
        const float Pitch = 52f, FieldOfView = 38f, CameraHeight = 11.5f;
        const float Friction = 4f;
        const int LevelsPerChunk = 8;
        const float ConvoyWindow = 22f;

        public Vector3[] Nodes { get; private set; }     // world position of each mission marker
        public bool InputEnabled = true;
        public bool Dragged { get; private set; }        // the last press moved the map (so it is not a tap)

        static Mesh[] chunkCache;
        static LevelPath roadCache;
        static Vector3[] nodeCache;

        Camera cam;
        CampaignConfig campaign;
        float scroll, velocity, minScroll, maxScroll;
        Vector2 pressPosition, lastPointer;
        bool pressing;
        LevelPath road;
        readonly List<Convoy> convoy = new List<Convoy>();
        readonly List<Transform> swaying = new List<Transform>();
        readonly List<Transform> spinning = new List<Transform>();
        Transform heli, heliRotor, heliTail;

        sealed class Convoy
        {
            public Transform root;
            public Transform[] wheels;
            public float offset;
        }

        public static WorldMapView Build(Transform parent, Camera cam, BalanceData balance, int[] stars)
        {
            var root = new GameObject("WorldMap");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<WorldMapView>();
            view.cam = cam;
            view.campaign = balance.campaign;
            view.Create(balance, stars);
            return view;
        }

        void Create(BalanceData balance, int[] stars)
        {
            int count = balance.levels.Length;
            if (chunkCache == null || nodeCache.Length != count) BuildStatic(balance);
            Nodes = nodeCache;
            road = roadCache;

            foreach (var mesh in chunkCache) Part(mesh, transform, Vector3.zero);

            // Flags: gold = won, blue = open, grey = locked.
            var flags = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                var at = Nodes[i];
                var color = stars[i] > 0 ? Art.Palette.Gold : Progress.Unlocked(i) ? Art.Hex("2F80ED") : Art.Hex("7D838A");
                flags.Cylinder(at + new Vector3(0.38f, 0.1f, 0.3f), 0.025f, 1.3f, 5, Art.Hex("D9DEE2"));
                flags.Box(at + new Vector3(0.6f, 1.22f, 0.3f), new Vector3(0.42f, 0.26f, 0.02f), color);
            }
            Part(flags.ToMesh("Flags"), transform, Vector3.zero);

            // Boss missions show their boss next to the marker.
            for (int i = 0; i < count; i++)
            {
                var boss = balance.levels[i].boss;
                if (boss == null) continue;
                var type = balance.Enemy(boss);
                var rig = Models.Rig(boss);
                var unit = Unit(rig, Nodes[i] + new Vector3(-0.2f, type.air ? 1.6f : 0f, -1.5f), 1.1f, out var parts);
                unit.localRotation = Quaternion.Euler(0f, 200f, 0f);
                (rig.Motion == Models.Motion.Turret || rig.Motion == Models.Motion.Walk ? swaying : spinning).AddRange(parts);
            }

            // A convoy near the view, and a helicopter crossing the sky.
            string[] vehicles = { "jeep", "tank", "apc", "jeep" };
            for (int i = 0; i < vehicles.Length; i++)
            {
                var root = Unit(Models.Rig(vehicles[i]), Vector3.zero, 0.75f, out var parts);
                convoy.Add(new Convoy { root = root, wheels = parts, offset = i * 1.3f });
            }
            heli = Unit(Models.Rig("heli"), Vector3.zero, 1f, out var heliParts);
            heliRotor = heliParts[0];
            heliTail = heliParts[1];

            minScroll = Nodes[0].x;
            maxScroll = Nodes[count - 1].x;
            Focus(Progress.Current);

            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 80f;
            cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;
            PlaceCamera();
        }

        // ---------------- Static map (built once) ----------------

        void BuildStatic(BalanceData balance)
        {
            int count = balance.levels.Length;
            nodeCache = new Vector3[count];
            var points = new List<Vector2> { new Vector2(-6f, 0f) };
            for (int i = 0; i < count; i++)
            {
                var p = new Vector2(i * Spacing, 2.6f * Mathf.Sin(i * 1.25f + 0.6f));
                points.Add(p);
                nodeCache[i] = new Vector3(p.x, 0f, p.y);
            }
            points.Add(new Vector2((count - 1) * Spacing + 5f, 0f));
            roadCache = new LevelPath(LevelPath.Rounded(points.ToArray(), 1.6f));
            road = roadCache;
            Nodes = nodeCache;

            int chunks = (count + LevelsPerChunk - 1) / LevelsPerChunk;
            chunkCache = new Mesh[chunks];
            var random = new System.Random(77);
            for (int c = 0; c < chunks; c++)
            {
                float x0 = c == 0 ? -12f : (c * LevelsPerChunk - 0.5f) * Spacing;
                float x1 = c == chunks - 1 ? count * Spacing + 8f : ((c + 1) * LevelsPerChunk - 0.5f) * Spacing;
                var b = new MeshBuilder();
                Terrain(b, x0, x1, random);
                // The road piece inside this chunk, overlapping a little so pieces join.
                var piece = new List<Vector2>();
                foreach (var p in road.Points) if (p.x >= x0 - 1f && p.x <= x1 + 1f) piece.Add(p);
                if (piece.Count >= 2) DirtRoad.Build(b, piece.ToArray(), 0.45f, 5 + c, StyleAt((x0 + x1) * 0.5f));
                Decor(b, random, x0, x1);
                for (int i = c * LevelsPerChunk; i < Mathf.Min(count, (c + 1) * LevelsPerChunk); i++) Models.Pad(b, Nodes[i]);
                if (c == 0)
                {
                    Models.Headquarters(b, new Vector3(-6.4f, 0f, 1.6f));
                    Models.Crates(b, new Vector3(-7.6f, 0f, -0.6f), 3);
                    Models.Barrels(b, new Vector3(-5f, 0f, -1.4f), 4);
                }
                chunkCache[c] = b.ToMesh("MapChunk" + c);
            }
        }

        // Zone of a map x position: the zone of the nearest mission.
        Biome BiomeAt(float x) => Campaign.BiomeOf(campaign, Mathf.Clamp(Mathf.RoundToInt(x / Spacing) + 1, 1, nodeCache.Length));

        BiomeStyle StyleAt(float x) => BiomeStyle.Of(BiomeAt(x));

        // Colour blended across zone borders.
        Color Blend(System.Func<BiomeStyle, Color> pick, float x)
        {
            var here = pick(StyleAt(x));
            var ahead = pick(StyleAt(x + Spacing));
            var behind = pick(StyleAt(x - Spacing));
            float f = Mathf.Repeat(x / Spacing + 0.5f, 1f);
            if (ahead != here) return Color.Lerp(here, ahead, Mathf.SmoothStep(0f, 0.5f, f));
            if (behind != here) return Color.Lerp(behind, here, Mathf.SmoothStep(0.5f, 1f, f));
            return here;
        }

        float Height(Vector2 p)
        {
            float clear = Mathf.InverseLerp(0.8f, 2.6f, road.DistanceTo(p));
            int nearest = Mathf.Clamp(Mathf.RoundToInt(p.x / Spacing), 0, Nodes.Length - 1);
            for (int i = Mathf.Max(0, nearest - 1); i <= Mathf.Min(Nodes.Length - 1, nearest + 1); i++)
                clear = Mathf.Min(clear, Mathf.InverseLerp(1.0f, 2.2f, (new Vector2(Nodes[i].x, Nodes[i].z) - p).magnitude));
            float hills = Mathf.PerlinNoise(p.x * 0.18f + 3f, p.y * 0.18f + 9f);
            float ridge = Mathf.Max(0f, Mathf.Abs(p.y) - 4.5f) * 0.35f;       // higher ground at the far and near edges
            float dunes = BiomeAt(p.x) == Biome.Desert ? Mathf.Sin(p.x * 0.9f + p.y * 0.4f) * 0.15f : 0f;
            return Mathf.Max(0f, hills * 1.6f - 0.4f + ridge + dunes) * clear;
        }

        void Terrain(MeshBuilder b, float x0, float x1, System.Random random)
        {
            const float cell = 0.9f, z0 = -10f, z1 = 12f;
            int cols = Mathf.CeilToInt((x1 - x0) / cell), rows = Mathf.CeilToInt((z1 - z0) / cell);
            var h = new float[cols + 1, rows + 1];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                    h[x, y] = Height(new Vector2(x0 + x * cell, z0 + y * cell));
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    float px = x0 + x * cell, pz = z0 + y * cell;
                    var p00 = new Vector3(px, h[x, y], pz); var p10 = new Vector3(px + cell, h[x + 1, y], pz);
                    var p01 = new Vector3(px, h[x, y + 1], pz + cell); var p11 = new Vector3(px + cell, h[x + 1, y + 1], pz + cell);
                    if (((x + y) & 1) == 0)
                    {
                        b.Triangle(p00, p01, p11, Ground(p00, p01, p11, random));
                        b.Triangle(p00, p11, p10, Ground(p00, p11, p10, random));
                    }
                    else
                    {
                        b.Triangle(p00, p01, p10, Ground(p00, p01, p10, random));
                        b.Triangle(p01, p11, p10, Ground(p01, p11, p10, random));
                    }
                }
        }

        Color Ground(Vector3 a, Vector3 b, Vector3 c, System.Random random)
        {
            var center = (a + b + c) / 3f;
            var n = Vector3.Cross(b - a, c - a).normalized;
            float m = Mathf.PerlinNoise(center.x * 0.25f + 5f, center.z * 0.25f + 1f);
            var col = Color.Lerp(Blend(s => s.GrassDark, center.x), Blend(s => s.GrassLight, center.x), Mathf.SmoothStep(0f, 1f, m));
            col = Color.Lerp(col, Blend(s => s.Slope, center.x), Mathf.InverseLerp(0.3f, 0.65f, 1f - Mathf.Abs(n.y)));
            col = Color.Lerp(col, Blend(s => s.HillTop, center.x), Mathf.InverseLerp(0.8f, 2f, center.y) * 0.6f);
            col *= 0.97f + 0.06f * (float)random.NextDouble();
            col.a = 1f;
            return col;
        }

        void Decor(MeshBuilder b, System.Random random, float x0, float x1)
        {
            int target = Mathf.RoundToInt((x1 - x0) * 2.6f);
            for (int attempt = 0, placed = 0; attempt < target * 6 && placed < target; attempt++)
            {
                var p = new Vector2(x0 + (float)random.NextDouble() * (x1 - x0), -9f + (float)random.NextDouble() * 20f);
                if (road.DistanceTo(p) < 1.1f) continue;
                int nearest = Mathf.Clamp(Mathf.RoundToInt(p.x / Spacing), 0, Nodes.Length - 1);
                if ((new Vector2(Nodes[nearest].x, Nodes[nearest].z) - p).magnitude < 2f) continue;
                if ((p - new Vector2(-6.4f, 1.6f)).magnitude < 2.5f) continue;
                var at = new Vector3(p.x, Height(p), p.y);
                var biome = BiomeAt(p.x);
                var style = BiomeStyle.Of(biome);
                int kind = random.Next(10), seed = random.Next();
                float size = 0.8f + 0.6f * (float)random.NextDouble();
                if (kind < 5) style.Tree(b, at, size, seed, biome);
                else if (kind < 8) style.Bush(b, at, size, seed, biome);
                else Models.Rock(b, at, size, seed, biome == Biome.Snow);
                placed++;
            }
        }

        // ---------------- Units ----------------

        Transform Unit(Models.UnitRig rig, Vector3 position, float scale, out Transform[] parts)
        {
            var root = new GameObject("Unit").transform;
            root.SetParent(transform, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            Part(rig.Body, root, Vector3.zero);
            parts = new Transform[rig.Parts.Length];
            for (int i = 0; i < parts.Length; i++) parts[i] = Part(rig.Parts[i], root, rig.Joints[i]);
            return root;
        }

        static Transform Part(Mesh mesh, Transform parent, Vector3 position)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;
            return go.transform;
        }

        // ---------------- Animation and scrolling ----------------

        void PlaceCamera()
        {
            var target = new Vector3(scroll, 0f, 0.6f);
            cam.transform.position = target - cam.transform.forward * (CameraHeight / Mathf.Sin(Pitch * Mathf.Deg2Rad));
            var sky = StyleAt(scroll).Sky;
            cam.backgroundColor = sky;
            RenderSettings.fogColor = sky;
        }

        // Distance along the road nearest to a map x (the road runs left to right).
        float DistanceAtX(float x)
        {
            float lo = 0f, hi = road.Length;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                int seg = 0;
                if (road.Sample(mid, ref seg, out _).x < x) lo = mid; else hi = mid;
            }
            return lo;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            HandleDrag(dt);
            PlaceCamera();

            float start = DistanceAtX(scroll) - ConvoyWindow * 0.5f;
            foreach (var c in convoy)
            {
                c.offset = Mathf.Repeat(c.offset + 0.9f * dt, ConvoyWindow);
                float d = Mathf.Clamp(start + c.offset, 0f, road.Length);
                int seg = 0;
                var p = road.Sample(d, ref seg, out var dir);
                c.root.localPosition = new Vector3(p.x, 0.03f, p.y);
                c.root.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 0f);
                // Jeeps and APCs have four wheels; the tank's single part is its turret and stays put.
                if (c.wheels.Length == 4)
                {
                    var spin = Quaternion.Euler(0f, 0f, -d * 420f);
                    foreach (var w in c.wheels) w.localRotation = spin;
                }
            }

            heli.localPosition = new Vector3(scroll - 14f + Mathf.Repeat(t * 2.2f, 28f), 3.2f + Mathf.Sin(t * 1.3f) * 0.2f, 3.5f + Mathf.Sin(t * 0.4f) * 1.5f);
            heli.localRotation = Quaternion.Euler(0f, 0f, -6f);
            heliRotor.localRotation = Quaternion.Euler(0f, t * 1300f, 0f);
            heliTail.localRotation = Quaternion.Euler(0f, 0f, t * 2000f);

            for (int i = 0; i < swaying.Count; i++)
                swaying[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.8f + i) * 30f, 0f);
            for (int i = 0; i < spinning.Count; i++)
                spinning[i].localRotation = Quaternion.Euler(0f, t * (i % 2 == 0 ? 900f : -900f), 0f);
        }

        void HandleDrag(float dt)
        {
            Vector2 pointer = Input.mousePosition;
            float worldPerPixel = 2f * CameraHeight / Mathf.Sin(Pitch * Mathf.Deg2Rad) * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect / Mathf.Max(Screen.width, 1);
            if (InputEnabled && Input.GetMouseButtonDown(0))
            {
                pressing = true;
                Dragged = false;
                velocity = 0f;
                pressPosition = lastPointer = pointer;
            }
            else if (pressing && Input.GetMouseButton(0))
            {
                if ((pointer - pressPosition).magnitude > 0.025f * Screen.height) Dragged = true;
                float delta = (pointer.x - lastPointer.x) * worldPerPixel;
                if (Dragged)
                {
                    scroll -= delta;
                    velocity = -delta / Mathf.Max(dt, 0.001f);
                }
                lastPointer = pointer;
            }
            else
            {
                pressing = false;
                scroll += velocity * dt;
                velocity = Mathf.MoveTowards(velocity, 0f, Mathf.Abs(velocity) * Friction * dt + 0.5f * dt);
            }
            scroll = Mathf.Clamp(scroll, minScroll, maxScroll);
        }

        // Scrolls so a mission marker is in view.
        public void Focus(int index) => scroll = Mathf.Clamp(Nodes[Mathf.Clamp(index, 0, Nodes.Length - 1)].x, minScroll, maxScroll);
    }
}
