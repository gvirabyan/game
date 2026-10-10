using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Sorting orders for everything in the world. All sprites share one atlas, so this is the only thing
    // that decides draw order.
    public static class Layers
    {
        public const int GroundPatch = 0, RoadEdge = 1, Road = 2, RoadMarks = 3, Decor = 4, Spot = 5, Range = 6;
        public const int TowerBase = 7, TowerPips = 8, GroundUnit = 9, Turret = 10, Headquarters = 10;
        public const int Projectile = 11, AirShadow = 12, AirUnit = 13, HealthBar = 14, Effect = 15;
    }

    // Builds the static part of a level (ground, road, build pads, base, decor) from its config.
    public static class LevelView
    {
        public static readonly float SpotPickRadius = 0.6f;
        const float HeadquartersSize = 1.6f;

        public static Transform Build(LevelConfig level, LevelPath path, float roadWidth, Rect view, int seed,
                                      List<BuildSpot> spots)
        {
            var root = new GameObject("Level " + level.name).transform;
            var random = new System.Random(seed);

            BuildGroundPatches(root, view, random);
            BuildRoad(root, path, roadWidth);

            spots.Clear();
            for (int i = 0; i < level.spots.Length; i++)
            {
                var marker = Sprite("Spot", Art.Spot, Color.white, Layers.Spot, root, level.spots[i], 0.95f);
                spots.Add(new BuildSpot { index = i, position = level.spots[i], marker = marker });
            }

            var hq = path.Points[path.Points.Length - 1];
            Sprite("Headquarters", Art.Headquarters, Color.white, Layers.Headquarters, root, hq, HeadquartersSize);

            BuildDecor(root, level, path, roadWidth, view, random, hq);
            return root;
        }

        public static SpriteRenderer Sprite(string name, Sprite sprite, Color color, int order, Transform parent,
                                            Vector2 position, float size, float angle = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size, size, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        static void BuildGroundPatches(Transform root, Rect view, System.Random random)
        {
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector2(Range(random, view.xMin, view.xMax), Range(random, view.yMin, view.yMax));
                bool darker = i % 2 == 0;
                var c = darker ? new Color(0.1f, 0.2f, 0.05f, 0.22f) : new Color(0.75f, 0.85f, 0.45f, 0.08f);
                Sprite("Patch", Art.Glow, c, Layers.GroundPatch, root, p, Range(random, 4f, 8f));
            }
        }

        static void BuildRoad(Transform root, LevelPath path, float width)
        {
            var pts = path.Points;
            var edge = Art.Palette.RoadEdge;
            var road = Art.Palette.Road;
            var marks = new Color(0f, 0f, 0f, 0.08f);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var dir = (b - a).normalized;
                var normal = new Vector2(-dir.y, dir.x);
                float len = (b - a).magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                var mid = (a + b) * 0.5f;

                Segment("RoadEdge", edge, Layers.RoadEdge, root, mid, len, width + 0.2f, angle);
                Segment("Road", road, Layers.Road, root, mid, len, width, angle);
                Segment("Track", marks, Layers.RoadMarks, root, mid + normal * (width * 0.22f), len, 0.07f, angle);
                Segment("Track", marks, Layers.RoadMarks, root, mid - normal * (width * 0.22f), len, 0.07f, angle);
            }
            // Round joints so corners look continuous.
            for (int i = 1; i < pts.Length - 1; i++)
            {
                Sprite("RoadJointEdge", Art.Disc, edge, Layers.RoadEdge, root, pts[i], width + 0.2f);
                Sprite("RoadJoint", Art.Disc, road, Layers.Road, root, pts[i], width);
            }
        }

        static void Segment(string name, Color color, int order, Transform parent, Vector2 mid, float length, float width, float angle)
        {
            var r = Sprite(name, Art.Pixel, color, order, parent, mid, 1f, angle);
            r.transform.localScale = new Vector3(length, width, 1f);
        }

        static void BuildDecor(Transform root, LevelConfig level, LevelPath path, float roadWidth, Rect view,
                               System.Random random, Vector2 hq)
        {
            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 34; attempt++)
            {
                int kind = random.Next(10);         // 0-4 tree, 5-7 bush, 8-9 rock
                float size = kind < 5 ? Range(random, 0.8f, 1.3f) : kind < 8 ? Range(random, 0.45f, 0.7f) : Range(random, 0.35f, 0.55f);
                var p = new Vector2(Range(random, view.xMin + 0.2f, view.xMax - 0.2f), Range(random, view.yMin + 0.2f, view.yMax - 0.2f));

                if (path.DistanceTo(p) < roadWidth * 0.5f + 0.15f + size * 0.5f) continue;
                if ((p - hq).magnitude < 1.2f + size * 0.5f) continue;
                bool blocked = false;
                foreach (var s in level.spots)
                    if ((p - s).magnitude < 0.75f + size * 0.5f) { blocked = true; break; }
                if (blocked) continue;

                var sprite = kind < 5 ? Art.Tree : kind < 8 ? Art.Bush : Art.Rock;
                // No rotation: the shadows are baked into the sprites and must all fall the same way.
                Sprite("Decor", sprite, Color.white, Layers.Decor, root, p, size);
                placed++;
            }
        }

        static float Range(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);
    }
}
