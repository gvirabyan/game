using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Main menu: a 3D campaign map. A dirt road winds through green hills, desert and snow, linking one marker per
    // mission; boss missions show their boss. A convoy drives the road, a helicopter crosses the sky, and the camera
    // scrolls sideways with a finger drag. GameUI puts the tappable level buttons over the markers.
    public sealed class WorldMapView : MonoBehaviour
    {
        const float Spacing = 4.4f;
        const float Pitch = 52f, FieldOfView = 38f, CameraHeight = 11.5f;
        const float Friction = 4f;
        static readonly Color Sky = Art.Hex("BCD3C8");

        // Biomes along the road, by mission index.
        static readonly Color[] GrassA = { Art.Hex("74A843"), Art.Hex("D8B878"), Art.Hex("E6ECF0") };
        static readonly Color[] GrassB = { Art.Hex("4A8436"), Art.Hex("C49A60"), Art.Hex("C8D3DC") };
        static readonly Color[] Slope = { Art.Hex("8C8471"), Art.Hex("B07A50"), Art.Hex("8A8F96") };

        public Vector3[] Nodes { get; private set; }     // world position of each mission marker
        public bool InputEnabled = true;
        public bool Dragged { get; private set; }        // the last press moved the map (so it is not a tap)

        Camera cam;
        float scroll, velocity, minScroll, maxScroll;
        Vector2 pressPosition, lastPointer;
        bool pressing;
        LevelPath road;
        readonly List<Convoy> convoy = new List<Convoy>();
        readonly List<Transform> swaying = new List<Transform>();      // boss turrets
        readonly List<Transform> spinning = new List<Transform>();     // boss rotors
        Transform heli, heliRotor, heliTail;
        int biomeSeed;

        sealed class Convoy
        {
            public Transform root;
            public Transform[] wheels;
            public float distance, speed;
        }

        public static WorldMapView Build(Transform parent, Camera cam, BalanceData balance, int[] stars)
        {
            var root = new GameObject("WorldMap");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<WorldMapView>();
            view.cam = cam;
            view.Create(balance, stars);
            return view;
        }

        // Which biome (0 green, 1 desert, 2 snow) a map x position belongs to, with blend weights.
        float Biome(float x) => Mathf.Clamp(x / (Spacing * 4f) - 0.25f, 0f, 2f);

        static Color Pick(Color[] set, float biome)
        {
            int i = Mathf.Min(1, (int)biome);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((biome - i) * 2f - 0.5f));
            return Color.Lerp(set[i], set[i + 1], t);
        }

        void Create(BalanceData balance, int[] stars)
        {
            int count = balance.levels.Length;
            Nodes = new Vector3[count];
            var points = new List<Vector2> { new Vector2(-6f, 0f) };
            for (int i = 0; i < count; i++)
            {
                var p = new Vector2(i * Spacing, 2.6f * Mathf.Sin(i * 1.25f + 0.6f));
                points.Add(p);
                Nodes[i] = new Vector3(p.x, 0f, p.y);
            }
            points.Add(new Vector2((count - 1) * Spacing + 5f, 0f));
            road = new LevelPath(LevelPath.Rounded(points.ToArray(), 1.6f));

            var random = new System.Random(77);
            var b = new MeshBuilder();
            Terrain(b, -12f, count * Spacing + 8f, random);
            DirtRoad.Build(b, road.Points, 0.45f, 5);
            Decor(b, random, count);

            // Start camp.
            Models.Headquarters(b, new Vector3(-6.4f, 0f, 1.6f));
            Models.Crates(b, new Vector3(-7.6f, 0f, -0.6f), 3);
            Models.Barrels(b, new Vector3(-5f, 0f, -1.4f), 4);

            for (int i = 0; i < count; i++)
            {
                var at = Nodes[i];
                Models.Pad(b, at);
                bool done = stars[i] > 0;
                // Flag: gold when the mission is won, blue otherwise.
                b.Cylinder(at + new Vector3(0.38f, 0.1f, 0.3f), 0.025f, 1.3f, 5, Art.Hex("D9DEE2"));
                b.Box(at + new Vector3(0.6f, 1.22f, 0.3f), new Vector3(0.42f, 0.26f, 0.02f), done ? Art.Palette.Gold : Art.Hex("2F80ED"));
            }

            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("WorldMap");
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;

            // Boss missions show their boss next to the marker.
            for (int i = 0; i < count; i++)
            {
                string boss = null;
                foreach (var w in balance.levels[i].waves)
                    foreach (var g in w.groups)
                        if (balance.Enemy(g.enemy).boss) boss = g.enemy;
                if (boss == null) continue;
                var type = balance.Enemy(boss);
                var unit = Unit(Models.Rig(boss), Nodes[i] + new Vector3(-0.2f, type.air ? 1.6f : 0f, -1.4f), 1.15f, out var parts);
                unit.localRotation = Quaternion.Euler(0f, 200f, 0f);
                var motion = Models.Rig(boss).Motion;
                (motion == Models.Motion.Turret ? swaying : spinning).AddRange(parts);
            }

            // A convoy patrolling the road, and a helicopter crossing the sky.
            string[] vehicles = { "jeep", "tank", "apc", "jeep" };
            for (int i = 0; i < vehicles.Length; i++)
            {
                var root = Unit(Models.Rig(vehicles[i]), Vector3.zero, 0.75f, out var parts);
                convoy.Add(new Convoy { root = root, wheels = parts, distance = road.Length * 0.15f + i * 1.3f, speed = 0.9f });
            }
            heli = Unit(Models.Rig("heli"), Vector3.zero, 1f, out var heliParts);
            heliRotor = heliParts[0];
            heliTail = heliParts[1];

            minScroll = Nodes[0].x;
            maxScroll = Nodes[count - 1].x;
            int next = System.Array.FindIndex(stars, s => s == 0);
            scroll = Mathf.Clamp(Nodes[next < 0 ? count - 1 : next].x, minScroll, maxScroll);

            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.backgroundColor = Sky;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 80f;
            cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;
            PlaceCamera();
        }

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

        // ---------------- Terrain ----------------

        float Height(Vector2 p)
        {
            float clear = Mathf.InverseLerp(0.8f, 2.6f, road.DistanceTo(p));
            foreach (var n in Nodes) clear = Mathf.Min(clear, Mathf.InverseLerp(1.0f, 2.2f, (new Vector2(n.x, n.z) - p).magnitude));
            float hills = Mathf.PerlinNoise(p.x * 0.18f + 3f, p.y * 0.18f + 9f);
            float ridge = Mathf.Max(0f, Mathf.Abs(p.y) - 4.5f) * 0.35f;       // higher ground at the far and near edges
            float bio = Biome(p.x);
            float dunes = bio > 0.6f && bio < 1.4f ? Mathf.Sin(p.x * 0.9f + p.y * 0.4f) * 0.15f : 0f;
            return Mathf.Max(0f, (hills * 1.6f - 0.4f + ridge + dunes)) * clear;
        }

        void Terrain(MeshBuilder b, float x0, float x1, System.Random random)
        {
            const float cell = 0.8f, z0 = -10f, z1 = 12f;
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
            float bio = Biome(center.x);
            float m = Mathf.PerlinNoise(center.x * 0.25f + 5f, center.z * 0.25f + 1f);
            var col = Color.Lerp(Pick(GrassB, bio), Pick(GrassA, bio), Mathf.SmoothStep(0f, 1f, m));
            col = Color.Lerp(col, Pick(Slope, bio), Mathf.InverseLerp(0.3f, 0.65f, 1f - Mathf.Abs(n.y)));
            if (bio > 1.5f) col = Color.Lerp(col, Color.white, Mathf.InverseLerp(0.8f, 2f, center.y) * 0.7f);   // snowy tops
            col *= 0.97f + 0.06f * (float)random.NextDouble();
            col.a = 1f;
            return col;
        }

        void Decor(MeshBuilder b, System.Random random, int count)
        {
            float x0 = -12f, x1 = count * Spacing + 8f;
            int placed = 0;
            for (int attempt = 0; attempt < 2500 && placed < 420; attempt++)
            {
                var p = new Vector2(x0 + (float)random.NextDouble() * (x1 - x0), -9f + (float)random.NextDouble() * 20f);
                if (road.DistanceTo(p) < 1.1f) continue;
                bool nearNode = false;
                foreach (var n in Nodes) if ((new Vector2(n.x, n.z) - p).magnitude < 1.9f) { nearNode = true; break; }
                if (nearNode || (p - new Vector2(-6.4f, 1.6f)).magnitude < 2.5f) continue;
                var at = new Vector3(p.x, Height(p), p.y);
                float bio = Biome(p.x);
                int kind = random.Next(10);
                int seed = random.Next();
                float size = 0.8f + 0.6f * (float)random.NextDouble();
                if (bio < 0.75f)
                {
                    if (kind < 5) Models.Tree(b, at, size, seed);
                    else if (kind < 7) Models.Bush(b, at, size, seed);
                    else if (kind < 8) Models.Rock(b, at, size, seed);
                    else Models.GrassTuft(b, at, size * 1.3f, seed);
                }
                else if (bio < 1.45f)
                {
                    if (kind < 3) Cactus(b, at, size, random);
                    else if (kind < 6) Models.Rock(b, at, size * 1.2f, seed);
                    else if (kind < 7) Models.Barrels(b, at, seed);
                    else DryBush(b, at, size);
                }
                else
                {
                    if (kind < 6) SnowPine(b, at, size);
                    else Models.Rock(b, at, size * 1.3f, seed);
                }
                placed++;
            }
        }

        static void Cactus(MeshBuilder b, Vector3 at, float size, System.Random random)
        {
            var green = Art.Hex("5E8C3A");
            b.Cylinder(at, 0.08f * size, 0.7f * size, 6, green, Art.Hex("78A84C"));
            if (random.Next(2) == 0)
            {
                b.Cylinder(at + new Vector3(0.12f, 0.3f, 0f) * size, 0.05f * size, 0.25f * size, 6, green, Art.Hex("78A84C"));
                b.Box(at + new Vector3(0.07f, 0.3f, 0f) * size, new Vector3(0.12f, 0.05f, 0.05f) * size, green);
            }
        }

        static void DryBush(MeshBuilder b, Vector3 at, float size) =>
            b.Sphere(at + new Vector3(0f, 0.1f * size, 0f), new Vector3(0.22f, 0.14f, 0.2f) * size, Art.Hex("9C8A54"), 2, 6, 0.3f, (int)(at.x * 100));

        static void SnowPine(MeshBuilder b, Vector3 at, float size)
        {
            var pine = Art.Hex("2E5A44");
            b.Cylinder(at, 0.07f * size, 0.3f * size, 5, Art.Hex("6B4A2F"));
            b.Cylinder(at + new Vector3(0f, 0.2f * size, 0f), 0.45f * size, 0.55f * size, 7, pine, null, 0f);
            b.Cylinder(at + new Vector3(0f, 0.5f * size, 0f), 0.34f * size, 0.5f * size, 7, MeshBuilder.Shade(pine, 1.1f), null, 0f);
            b.Cylinder(at + new Vector3(0f, 0.78f * size, 0f), 0.2f * size, 0.38f * size, 7, Art.Hex("F2F6F8"), null, 0f);
        }

        // ---------------- Animation and scrolling ----------------

        void PlaceCamera()
        {
            var forward = cam.transform.forward;
            // Look at a point on the road line; the camera sits back along the view direction.
            var target = new Vector3(scroll, 0f, 0.6f);
            cam.transform.position = target - forward * (CameraHeight / Mathf.Sin(Pitch * Mathf.Deg2Rad));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            HandleDrag(dt);
            PlaceCamera();

            foreach (var c in convoy)
            {
                c.distance = (c.distance + c.speed * dt) % road.Length;
                int seg = 0;
                var p = road.Sample(c.distance, ref seg, out var dir);
                c.root.localPosition = new Vector3(p.x, 0.03f, p.y);
                c.root.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 0f);
                // Jeeps and APCs have four wheels; the tank's single part is its turret and stays put.
                if (c.wheels.Length == 4)
                {
                    var spin = Quaternion.Euler(0f, 0f, -c.distance * 420f);
                    foreach (var w in c.wheels) w.localRotation = spin;
                }
            }

            float span = maxScroll - minScroll + 20f;
            float hx = minScroll - 10f + Mathf.Repeat(t * 2.2f, span);
            heli.localPosition = new Vector3(hx, 3.2f + Mathf.Sin(t * 1.3f) * 0.2f, 3.5f + Mathf.Sin(t * 0.4f) * 1.5f);
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

        // Scrolls so a mission marker is in view (used when returning from a level).
        public void Focus(int index) => scroll = Mathf.Clamp(Nodes[index].x, minScroll, maxScroll);
    }
}
