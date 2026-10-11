using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // 3D view setup. Game logic works in flat 2D "map" coordinates (x, y), exactly as levels are authored.
    // A low perspective camera tilted by `Pitch` looks at the battlefield. Map positions are stretched along Z
    // by `ZStretch` so the foreshortened map still roughly keeps its authored proportions on screen, while
    // models keep their real proportions. CameraRig fits the camera so the whole play area is visible.
    public static class View
    {
        public const float Pitch = 42f;
        public const float FieldOfView = 32f;
        public const float ZStretch = 1f;

        // Map -> world stretch for flat geometry built in map coordinates (ground, road).
        public static readonly Matrix4x4 GroundTransform = Matrix4x4.Scale(new Vector3(1f, 1f, ZStretch));

        // Map point + height -> world position.
        public static Vector3 World(Vector2 p, float height = 0f) => new Vector3(p.x, height, p.y * ZStretch);

        // Logic angle (degrees, counter-clockwise from +x on the map) -> rotation around the vertical axis,
        // so a model faces the same way on screen as the logic direction.
        public static Quaternion Yaw(float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            return Quaternion.Euler(0f, -Mathf.Atan2(Mathf.Sin(a) * ZStretch, Mathf.Cos(a)) * Mathf.Rad2Deg, 0f);
        }
    }

    // Places the perspective camera so a map-space rectangle fills the screen below the HUD.
    public static class CameraRig
    {
        // The play area every level must fit into (pads, road, base and the height of models).
        public static readonly Rect PlayArea = new Rect(-8.7f, -5.1f, 17.4f, 9.6f);
        // Fraction of the screen height covered by the HUD row at the top.
        public const float HudFraction = 0.13f;

        public static readonly Color Haze = Art.Hex("BCD3C8");

        public static void Fit(Camera cam)
        {
            cam.orthographic = false;
            cam.backgroundColor = Haze;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            cam.fieldOfView = View.FieldOfView;
            var rot = Quaternion.Euler(View.Pitch, 0f, 0f);
            cam.transform.rotation = rot;
            var forward = rot * Vector3.forward;

            var corners = new[]
            {
                View.World(new Vector2(PlayArea.xMin, PlayArea.yMin)), View.World(new Vector2(PlayArea.xMax, PlayArea.yMin)),
                View.World(new Vector2(PlayArea.xMin, PlayArea.yMax), 0.8f), View.World(new Vector2(PlayArea.xMax, PlayArea.yMax), 0.8f),
            };
            float top = 1f - HudFraction;
            float centerZ = View.World(PlayArea.center).z, distance = 30f;
            float depth = PlayArea.height * View.ZStretch;
            // Converges in a few steps: scale the distance to fit, then shift to centre vertically.
            for (int i = 0; i < 30; i++)
            {
                cam.transform.position = new Vector3(0f, 0f, centerZ) - forward * distance;
                float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
                foreach (var c in corners)
                {
                    var v = cam.WorldToViewportPoint(c);
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                float scale = Mathf.Max((maxX - minX) / 1f, (maxY - minY) / top);
                distance *= Mathf.Lerp(1f, scale, 0.7f);
                float shift = (minY + maxY) * 0.5f - top * 0.5f;
                centerZ += shift * depth / Mathf.Max(maxY - minY, 0.05f) * 0.7f;
            }
            cam.transform.position = new Vector3(0f, 0f, centerZ) - forward * distance;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = distance * 3f;
            RenderSettings.fogStartDistance = distance * 1.05f;
            RenderSettings.fogEndDistance = distance * 2.4f;
        }

        // Map-space bounds of what the camera sees on the ground (the far edge is wider than the near one).
        public static Rect VisibleMap(Camera cam)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
            {
                var ray = cam.ViewportPointToRay(v);
                if (!ground.Raycast(ray, out float enter)) enter = cam.farClipPlane;
                var p = ray.GetPoint(enter);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.z / View.ZStretch); maxY = Mathf.Max(maxY, p.z / View.ZStretch);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }

    // Sorting orders for the few sprites still in the world (range ring, health bars, effects).
    public static class Layers
    {
        public const int Range = 0, HealthBar = 10, Effect = 20;
    }

    // Builds the static part of a level (terrain, road, build pads, base, props) as one combined mesh.
    public static class LevelView
    {
        public static readonly float SpotPickRadius = 0.6f;

        public static Transform Build(LevelConfig level, LevelPath path, float roadWidth, Rect view, int seed,
                                      Transform worldRoot, List<BuildSpot> spots)
        {
            var root = new GameObject("Level " + level.name).transform;
            root.SetParent(worldRoot, false);
            var site = new Site(level, path, roadWidth, seed);
            var b = new MeshBuilder();

            // Flat parts are built in map coordinates and stretched; everything with height keeps its proportions.
            b.Transform = View.GroundTransform;
            site.Terrain(b, view);
            site.Road(b);
            b.Transform = Matrix4x4.identity;

            spots.Clear();
            for (int i = 0; i < level.spots.Length; i++)
            {
                Models.Pad(b, View.World(level.spots[i]));
                spots.Add(new BuildSpot { index = i, position = level.spots[i] });
            }
            Models.Headquarters(b, View.World(site.Hq));
            site.Props(b, view);

            var go = new GameObject("StaticLevel");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Level " + level.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;
            return root;
        }

        // One level's terrain shape and prop placement rules.
        sealed class Site
        {
            static readonly Color GrassLight = Art.Hex("74A843"), GrassDark = Art.Hex("4A8436"), GrassDry = Art.Hex("A0A852");
            static readonly Color HillRock = Art.Hex("9A8F78"), HillTop = Art.Hex("8DBA55");
            const float Cell = 0.7f;

            public readonly Vector2 Hq;
            readonly LevelConfig level;
            readonly LevelPath path;
            readonly float roadHalf;
            readonly System.Random random;
            readonly float nx, ny;
            readonly List<Vector3> taken = new List<Vector3>();      // x, y = map position, z = radius

            public Site(LevelConfig level, LevelPath path, float roadWidth, int seed)
            {
                this.level = level;
                this.path = path;
                roadHalf = roadWidth * 0.5f;
                random = new System.Random(seed);
                nx = (float)random.NextDouble() * 100f;
                ny = (float)random.NextDouble() * 100f;
                Hq = path.Points[path.Points.Length - 1];
            }

            // Flat battlefield framed by hills beyond the play area; flattened under the road, pads and base.
            public float Height(Vector2 p)
            {
                var a = CameraRig.PlayArea;
                float dx = Mathf.Max(a.xMin - p.x, p.x - a.xMax, 0f);
                float dy = Mathf.Max(p.y - a.yMax + 0.4f, 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Mathf.PerlinNoise(p.x * 0.33f + nx, p.y * 0.33f + ny);
                float hill = Mathf.Min(outside * 0.6f, 3.2f) * (0.45f + n);
                float bumps = Mathf.Max(0f, Mathf.PerlinNoise(p.x * 0.8f + ny, p.y * 0.8f + nx) - 0.55f) * 0.35f;

                float clear = Mathf.InverseLerp(roadHalf + 0.3f, roadHalf + 1.8f, path.DistanceTo(p));
                clear = Mathf.Min(clear, Mathf.InverseLerp(1.6f, 2.8f, (p - Hq).magnitude));
                foreach (var s in level.spots) clear = Mathf.Min(clear, Mathf.InverseLerp(0.6f, 1.1f, (p - s).magnitude));
                return (hill + bumps) * clear;
            }

            public void Terrain(MeshBuilder b, Rect view)
            {
                float x0 = view.xMin - 2f, y0 = view.yMin - 2f;
                int cols = Mathf.CeilToInt((view.width + 4f) / Cell), rows = Mathf.CeilToInt((view.height + 5f) / Cell);
                var h = new float[cols + 1, rows + 1];
                for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                    h[x, y] = Height(new Vector2(x0 + x * Cell, y0 + y * Cell));

                for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    float px = x0 + x * Cell, py = y0 + y * Cell;
                    var p00 = new Vector3(px, h[x, y], py); var p10 = new Vector3(px + Cell, h[x + 1, y], py);
                    var p01 = new Vector3(px, h[x, y + 1], py + Cell); var p11 = new Vector3(px + Cell, h[x + 1, y + 1], py + Cell);
                    // Alternate the diagonal so the facets do not line up into stripes.
                    if (((x + y) & 1) == 0)
                    {
                        b.Triangle(p00, p01, p11, GroundColor(p00, p01, p11));
                        b.Triangle(p00, p11, p10, GroundColor(p00, p11, p10));
                    }
                    else
                    {
                        b.Triangle(p00, p01, p10, GroundColor(p00, p01, p10));
                        b.Triangle(p01, p11, p10, GroundColor(p01, p11, p10));
                    }
                }
            }

            Color GroundColor(Vector3 a, Vector3 b, Vector3 c)
            {
                var center = (a + b + c) / 3f;
                var n = Vector3.Cross(b - a, c - a).normalized;
                float m = Mathf.PerlinNoise(center.x * 0.22f + nx + 7f, center.z * 0.22f + ny + 3f);
                var col = Color.Lerp(GrassDark, GrassLight, Mathf.SmoothStep(0f, 1f, m));
                float dry = Mathf.PerlinNoise(center.x * 0.45f + nx + 21f, center.z * 0.45f + ny + 11f);
                col = Color.Lerp(col, GrassDry, Mathf.InverseLerp(0.6f, 0.8f, dry) * 0.75f);
                col = Color.Lerp(col, HillRock, Mathf.InverseLerp(0.3f, 0.65f, 1f - Mathf.Abs(n.y)));
                col = Color.Lerp(col, HillTop, Mathf.InverseLerp(1.2f, 3f, center.y) * 0.5f);     // sunlit hilltops
                col *= 0.97f + 0.06f * (float)random.NextDouble();
                col.a = 1f;
                return col;
            }

            public void Road(MeshBuilder b) => DirtRoad.Build(b, path.Points, roadHalf, random.Next());

            public void Props(MeshBuilder b, Rect view)
            {
                var area = Rect.MinMaxRect(view.xMin - 1f, view.yMin, view.xMax + 1f, view.yMax + 3f);

                // Pebbles along the road edges.
                for (int i = 0; i < path.Points.Length - 1; i++)
                {
                    var a = path.Points[i];
                    var d = path.Points[i + 1] - a;
                    var dir = d.normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    for (float t = 0.3f; t < d.magnitude; t += 0.45f)
                    {
                        if (random.NextDouble() < 0.45) continue;
                        float side = random.Next(2) == 0 ? 1f : -1f;
                        var p = a + dir * t + normal * side * (roadHalf + 0.12f + Range(0f, 0.15f));
                        if (path.DistanceTo(p) < roadHalf + 0.08f) continue;
                        Models.Rock(b, View.World(p), Range(0.25f, 0.42f), random.Next());
                    }
                }

                // Base camp around the HQ.
                PlaceNear(Hq, 1.4f, 2.2f, 4, 0.35f, p => Models.Crates(b, Ground(p), random.Next()));
                PlaceNear(Hq, 1.3f, 2.2f, 3, 0.3f, p => Models.Barrels(b, Ground(p), random.Next()));

                // Military clutter on the battlefield and tank traps beside the road.
                Scatter(area, 6, 0.4f, false, true, p => Models.Crates(b, Ground(p), random.Next()));
                Scatter(area, 5, 0.35f, false, true, p => Models.Barrels(b, Ground(p), random.Next()));
                Scatter(area, 5, 0.6f, false, true, p => Models.Sandbags(b, Ground(p), Range(0f, 180f)));
                for (int i = 0, placed = 0; i < 200 && placed < 6; i++)
                {
                    int seg = 0;
                    var on = path.Sample(Range(0f, path.Length), ref seg, out var dir);
                    var p = on + new Vector2(-dir.y, dir.x) * (random.Next(2) == 0 ? 1f : -1f) * (roadHalf + Range(0.45f, 0.8f));
                    if (!CameraRig.PlayArea.Contains(p) || !Free(p, 0.25f, false)) continue;
                    Models.TankTrap(b, Ground(p), Range(0f, 90f));
                    Take(p, 0.25f);
                    placed++;
                }

                // Vegetation: trees mostly on the hills, bushes, rocks, grass and flowers on the field.
                Scatter(area, 70, 0.42f, true, false, p => Models.Tree(b, Ground(p), Range(0.9f, 1.4f), random.Next()));
                Scatter(area, 30, 0.3f, false, false, p => Models.Bush(b, Ground(p), Range(0.8f, 1.2f), random.Next()));
                Scatter(area, 16, 0.3f, false, false, p => Models.Rock(b, Ground(p), Range(0.7f, 1.2f), random.Next()));
                Scatter(area, 320, 0f, false, false, p => Models.GrassTuft(b, Ground(p), Range(0.7f, 1.3f), random.Next()));
                Scatter(area, 90, 0f, false, false, p => Models.Flower(b, Ground(p), random.Next()));
            }

            Vector3 Ground(Vector2 p) => View.World(p, Height(p));

            void Scatter(Rect area, int count, float radius, bool tall, bool fieldOnly, System.Action<Vector2> place)
            {
                for (int attempt = 0, placed = 0; attempt < count * 12 && placed < count; attempt++)
                {
                    var p = new Vector2(Range(area.xMin, area.xMax), Range(area.yMin, area.yMax));
                    bool onField = CameraRig.PlayArea.Contains(p);
                    if (fieldOnly && !onField) continue;
                    // Few trees on the battlefield itself so they do not hide the action.
                    if (tall && onField && random.NextDouble() < 0.75) continue;
                    if (!Free(p, radius, tall)) continue;
                    place(p);
                    if (radius > 0f) Take(p, radius);
                    placed++;
                }
            }

            void PlaceNear(Vector2 center, float minR, float maxR, int count, float radius, System.Action<Vector2> place)
            {
                for (int attempt = 0, placed = 0; attempt < count * 20 && placed < count; attempt++)
                {
                    float a = Range(0f, Mathf.PI * 2f);
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(minR, maxR);
                    if (!Free(p, radius, false)) continue;
                    place(p);
                    Take(p, radius);
                    placed++;
                }
            }

            bool Free(Vector2 p, float radius, bool tall)
            {
                if (path.DistanceTo(p) < roadHalf + 0.12f + radius) return false;
                if ((p - Hq).magnitude < 1.15f + radius) return false;
                foreach (var s in level.spots)
                {
                    if ((p - s).magnitude < 0.62f + radius) return false;
                    // Tall props in front of (below) a pad or the base would hide it.
                    if (tall && Mathf.Abs(s.x - p.x) < 0.9f && s.y - p.y > 0f && s.y - p.y < 2.2f) return false;
                }
                if (tall && Mathf.Abs(Hq.x - p.x) < 1.3f && Hq.y - p.y > 0f && Hq.y - p.y < 2.4f) return false;
                foreach (var t in taken)
                    if ((p - new Vector2(t.x, t.y)).magnitude < radius + t.z) return false;
                return true;
            }

            void Take(Vector2 p, float radius) => taken.Add(new Vector3(p.x, p.y, radius));

            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        }

    }

    // An unpaved track: a ribbon along the path with ragged, wobbling edges, a worn grass verge,
    // patchy dirt colour, darker wheel ruts and the odd puddle. Built in map coordinates.
    public static class DirtRoad
    {
        static readonly Color DirtLight = Art.Hex("C7A06A"), DirtDark = Art.Hex("A07E50"), Verge = Art.Hex("7E7A44");
        static readonly Color Rut = Art.Hex("8C6C44"), Puddle = Art.Hex("6E7A82");
        const float Step = 0.22f;

        public static void Build(MeshBuilder b, Vector2[] points, float half, int seed)
        {
            var random = new System.Random(seed);
            float nx = (float)random.NextDouble() * 100f, ny = (float)random.NextDouble() * 100f;
            var path = new LevelPath(points);
            int n = Mathf.Max(2, Mathf.CeilToInt(path.Length / Step) + 1);
            var center = new Vector2[n];
            var side = new Vector2[n];
            var left = new float[n];
            var right = new float[n];
            int seg = 0;
            for (int i = 0; i < n; i++)
            {
                float d = Mathf.Min(i * Step, path.Length);
                center[i] = path.Sample(d, ref seg, out var dir);
                side[i] = new Vector2(-dir.y, dir.x);
                // Each edge wobbles on its own, so the width changes too.
                left[i] = half * (1f + 0.3f * (Mathf.PerlinNoise(d * 0.7f + nx, ny) - 0.5f)) + 0.12f * (Mathf.PerlinNoise(d * 3.5f + nx, 7f) - 0.5f);
                right[i] = half * (1f + 0.3f * (Mathf.PerlinNoise(d * 0.7f + ny, nx) - 0.5f)) + 0.12f * (Mathf.PerlinNoise(d * 3.5f + ny, 9f) - 0.5f);
            }
            // Corners need the side vector averaged or the strip folds over itself.
            for (int i = 1; i < n - 1; i++) side[i] = (side[i - 1] + side[i] + side[i + 1]).normalized;

            for (int i = 0; i < n - 1; i++)
            {
                float d = i * Step;
                // Worn verge of uneven width on each side.
                float vl0 = 0.1f + 0.3f * Mathf.PerlinNoise(d * 1.3f + nx, 21f), vl1 = 0.1f + 0.3f * Mathf.PerlinNoise((d + Step) * 1.3f + nx, 21f);
                float vr0 = 0.1f + 0.3f * Mathf.PerlinNoise(d * 1.3f + ny, 23f), vr1 = 0.1f + 0.3f * Mathf.PerlinNoise((d + Step) * 1.3f + ny, 23f);
                Strip(b, center, side, i, left[i] + vl0, -(right[i] + vr0), left[i + 1] + vl1, -(right[i + 1] + vr1), 0.008f, Verge);
                var dirt = Color.Lerp(DirtLight, DirtDark, Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(d * 0.45f + nx, ny + 3f)));
                dirt *= 0.97f + 0.06f * (float)random.NextDouble();
                dirt.a = 1f;
                Strip(b, center, side, i, left[i], -right[i], left[i + 1], -right[i + 1], 0.02f, dirt);
                // Broken wheel ruts that drift, fade in and out, and a tuft of grass here and there in the middle.
                float drift = 0.14f * (Mathf.PerlinNoise(d * 0.6f + 11f, nx) - 0.5f);
                float rut = Mathf.PerlinNoise(d * 0.5f + 31f, ny);
                if (rut > 0.45f)
                {
                    var rutColor = Color.Lerp(dirt, Rut, Mathf.InverseLerp(0.45f, 0.75f, rut) * 0.7f);
                    foreach (float o in new[] { 0.5f, -0.5f })
                    {
                        float w = 0.035f + 0.03f * Mathf.PerlinNoise(d * 2f + o * 10f, 5f);
                        Strip(b, center, side, i, half * o + drift + w, half * o + drift - w, half * o + drift + w, half * o + drift - w, 0.026f, rutColor);
                    }
                }
            }
            // Grass islands between the ruts.
            for (int i = 3; i < n - 3; i += 5 + random.Next(9))
                if (Mathf.PerlinNoise(i * Step * 0.5f + 31f, ny) < 0.4f)
                    Blob(b, center[i] + side[i] * (((float)random.NextDouble() - 0.5f) * 0.2f), 0.07f + 0.06f * (float)random.NextDouble(), 0.024f, Verge, random);
            // A few puddles and stones on the track.
            for (int i = 4; i < n - 4; i += 9 + random.Next(14))
            {
                var p = center[i] + side[i] * (((float)random.NextDouble() - 0.5f) * half);
                if (random.Next(3) == 0)
                    Blob(b, p, 0.16f + 0.12f * (float)random.NextDouble(), 0.03f, Puddle, random);
                else
                    b.Sphere(new Vector3(p.x, 0.03f, p.y), new Vector3(0.06f, 0.035f, 0.05f), Art.Hex("8E8A80"), 2, 5, 0.2f, random.Next());
            }
        }

        // One quad of a ribbon between cross-sections i and i+1; offsets are along the side vector (left positive).
        static void Strip(MeshBuilder b, Vector2[] c, Vector2[] s, int i, float l0, float r0, float l1, float r1, float y, Color color)
        {
            Vector3 P(Vector2 p) => new Vector3(p.x, y, p.y);
            b.Quad(P(c[i] + s[i] * l0), P(c[i + 1] + s[i + 1] * l1), P(c[i + 1] + s[i + 1] * r1), P(c[i] + s[i] * r0), color);
        }

        // Irregular flat disc.
        static void Blob(MeshBuilder b, Vector2 at, float radius, float y, Color color, System.Random random)
        {
            const int sides = 9;
            var center = new Vector3(at.x, y, at.y);
            var rim = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides;
                float r = radius * (0.75f + 0.5f * (float)random.NextDouble());
                rim[k] = center + new Vector3(Mathf.Cos(a) * r * 1.4f, 0f, Mathf.Sin(a) * r);
            }
            for (int k = 0; k < sides; k++)
                b.Triangle(center, rim[(k + 1) % sides], rim[k], color);
        }
    }
}

