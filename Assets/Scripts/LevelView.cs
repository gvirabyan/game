using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // 2.5D view setup. Game logic works in flat 2D "map" coordinates (x, y), exactly as levels are authored.
    // The 3D world root is stretched along Z by 1/sin(pitch) and seen by an orthographic camera tilted by
    // `Pitch`, so the ground looks exactly like the 2D map while models show their height and sides.
    public static class View
    {
        public const float Pitch = 50f;
        public static readonly float ZStretch = 1f / Mathf.Sin(Pitch * Mathf.Deg2Rad);

        // Map point + height -> local position under the stretched world root.
        public static Vector3 Local(Vector2 p, float height = 0f) => new Vector3(p.x, height, p.y);

        // Map point + height -> real world position (for billboards that live outside the stretched root).
        public static Vector3 World(Vector2 p, float height = 0f) => new Vector3(p.x, height, p.y * ZStretch);

        // Logic angle (degrees, counter-clockwise from +x on the map) -> rotation around the vertical axis.
        public static Quaternion Yaw(float angle) => Quaternion.Euler(0f, -angle, 0f);
    }

    // Sorting orders for the few sprites still in the world (range ring, health bars, effects).
    public static class Layers
    {
        public const int Range = 0, HealthBar = 10, Effect = 20;
    }

    // Builds the static part of a level (ground, road, build pads, base, decor) as one combined mesh.
    public static class LevelView
    {
        public static readonly float SpotPickRadius = 0.6f;

        static readonly Color GroundA = Art.Hex("5B7F45"), GroundB = Art.Hex("54773F");
        static readonly Color RoadColor = Art.Hex("D2BE92"), RoadEdgeColor = Art.Hex("A48F62"), TrackColor = Art.Hex("BBA67A");

        public static Transform Build(LevelConfig level, LevelPath path, float roadWidth, Rect view, int seed,
                                      Transform worldRoot, List<BuildSpot> spots)
        {
            var root = new GameObject("Level " + level.name).transform;
            root.SetParent(worldRoot, false);
            var random = new System.Random(seed);
            var b = new MeshBuilder();

            BuildGround(b, view, random);
            BuildRoad(b, path, roadWidth);

            spots.Clear();
            for (int i = 0; i < level.spots.Length; i++)
            {
                Models.Pad(b, View.Local(level.spots[i]));
                spots.Add(new BuildSpot { index = i, position = level.spots[i] });
            }

            var hq = path.Points[path.Points.Length - 1];
            Models.Headquarters(b, View.Local(hq));
            BuildDecor(b, level, path, roadWidth, view, random, hq);

            var go = new GameObject("StaticLevel");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Level " + level.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;
            return root;
        }

        // Faceted ground: a grid of slightly differently coloured triangles, larger than any screen.
        static void BuildGround(MeshBuilder b, Rect view, System.Random random)
        {
            float x0 = Mathf.Floor(view.xMin) - 3f, x1 = Mathf.Ceil(view.xMax) + 3f;
            float y0 = Mathf.Floor(view.yMin) - 3f, y1 = Mathf.Ceil(view.yMax) + 6f;
            const float cell = 1.25f;
            for (float y = y0; y < y1; y += cell)
            for (float x = x0; x < x1; x += cell)
            {
                var p00 = new Vector3(x, 0f, y); var p10 = new Vector3(x + cell, 0f, y);
                var p01 = new Vector3(x, 0f, y + cell); var p11 = new Vector3(x + cell, 0f, y + cell);
                b.Triangle(p00, p01, p11, Vary(random));
                b.Triangle(p00, p11, p10, Vary(random));
            }
        }

        static Color Vary(System.Random r) => Color.Lerp(GroundA, GroundB, (float)r.NextDouble()) * (0.96f + 0.08f * (float)r.NextDouble());

        static void BuildRoad(MeshBuilder b, LevelPath path, float width)
        {
            var pts = path.Points;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i];
                var c = pts[i + 1];
                var dir = (c - a).normalized;
                var normal = new Vector2(-dir.y, dir.x);
                float len = (c - a).magnitude;
                float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                var mid = (a + c) * 0.5f;

                b.Box(View.Local(mid, 0.015f), new Vector3(len, 0.03f, width + 0.24f), RoadEdgeColor, yaw);
                b.Box(View.Local(mid, 0.03f), new Vector3(len, 0.03f, width), RoadColor, yaw);
                b.Box(View.Local(mid + normal * (width * 0.24f), 0.046f), new Vector3(len, 0.004f, 0.08f), TrackColor, yaw);
                b.Box(View.Local(mid - normal * (width * 0.24f), 0.046f), new Vector3(len, 0.004f, 0.08f), TrackColor, yaw);
            }
            // Rounded joints so corners look continuous.
            for (int i = 1; i < pts.Length - 1; i++)
            {
                b.Cylinder(View.Local(pts[i]), (width + 0.24f) * 0.5f, 0.03f, 16, RoadEdgeColor);
                b.Cylinder(View.Local(pts[i]), width * 0.5f, 0.0455f, 16, RoadColor);
            }
        }

        static void BuildDecor(MeshBuilder b, LevelConfig level, LevelPath path, float roadWidth, Rect view,
                               System.Random random, Vector2 hq)
        {
            int placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 40; attempt++)
            {
                int kind = random.Next(10);         // 0-4 tree, 5-7 bush, 8-9 rock
                float size = kind < 5 ? Range(random, 0.9f, 1.35f) : kind < 8 ? Range(random, 0.8f, 1.2f) : Range(random, 0.7f, 1.2f);
                float radius = (kind < 5 ? 0.42f : 0.34f) * size;
                var p = new Vector2(Range(random, view.xMin + 0.2f, view.xMax - 0.2f), Range(random, view.yMin + 0.2f, view.yMax + 2f));

                if (path.DistanceTo(p) < roadWidth * 0.5f + 0.15f + radius) continue;
                if ((p - hq).magnitude < 1.2f + radius) continue;
                bool blocked = false;
                foreach (var s in level.spots)
                    if ((p - s).magnitude < 0.75f + radius) { blocked = true; break; }
                // Tall props are drawn shifted up the screen; keep them out of the band right below a pad
                // or the base so they never hide a tower.
                if (kind < 5)
                {
                    foreach (var s in level.spots)
                        if (Mathf.Abs(s.x - p.x) < 0.9f && s.y - p.y > 0f && s.y - p.y < 1.8f) { blocked = true; break; }
                    if (Mathf.Abs(hq.x - p.x) < 1.3f && hq.y - p.y > 0f && hq.y - p.y < 2f) blocked = true;
                }
                if (blocked) continue;

                int seed = random.Next();
                if (kind < 5) Models.Tree(b, View.Local(p), size, seed);
                else if (kind < 8) Models.Bush(b, View.Local(p), size, seed);
                else Models.Rock(b, View.Local(p), size, seed);
                placed++;
            }
        }

        static float Range(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);
    }
}
