using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Enemies, projectiles and effects are plain objects driven by Game.Update, not MonoBehaviours,
    // so 200+ of them cost one Update call instead of hundreds.

    public sealed class Enemy
    {
        public const int PartCount = 4;
        public Transform root;              // positioned, rotated and scaled; holds the body and the parts
        public MeshFilter body;
        public readonly Transform[] parts = new Transform[PartCount];     // legs, turret, wheels or rotors
        public readonly MeshFilter[] partFilters = new MeshFilter[PartCount];
        public readonly MeshRenderer[] renderers = new MeshRenderer[PartCount + 1];    // body first
        public Models.UnitRig rig;          // which model the body and parts are set up for
        public float flash;                 // hit flash, 1 right after a hit, fades to 0
        public Transform hpRoot, hpFill;    // billboard under the overlay root
        public SpriteRenderer hpBackRenderer, hpFillRenderer;
        public EnemyConfig type;
        public Vector2 position;
        public float distance;              // travelled along the path
        public int segment;                 // current path segment, only ever increases
        public float health, maxHealth, angle, height;
        public float slow, slowTime;        // speed reduction share and how long it lasts
        public float burnDps, burnTime, burnFx;
        public int generation;              // bumps on every release so stale references can be detected
        public int activeIndex;
    }

    public sealed class Projectile
    {
        public Transform transform;
        public MeshFilter filter;
        public Tower source;
        public float damage;
        public Enemy target;
        public int targetGeneration;
        public Vector2 position, aim, start;
        public float height, startHeight;
        public bool arc;                    // mortar shells fly a high arc to a fixed point
        public int activeIndex;
    }

    // A short-lived line: tesla lightning or a sniper tracer.
    public sealed class Bolt
    {
        public LineRenderer line;
        public float age, life;
        public Color color;
        public int activeIndex;
    }

    // A flat ring expanding on the ground (cryo pulse).
    public sealed class Ripple
    {
        public SpriteRenderer renderer;
        public float age, life, radius;
        public Color color;
        public int activeIndex;
    }

    public sealed class Effect
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float age, life, startSize, endSize;
        public Vector3 velocity;
        public Color color;
        public int activeIndex;
    }

    public sealed class Tower
    {
        public TowerConfig type;
        public int level;
        public int spent;
        public float cooldown, angle, recoil;
        public float bounce;                // 1 right after building or upgrading, fades to 0
        public GameObject root;
        public MeshFilter baseFilter, turretFilter;
        public Transform turret;
        public GameObject[] pips;
        public Enemy target;
        public int targetGeneration;
    }

    public sealed class BuildSpot
    {
        public int index;
        public Vector2 position;
        public Tower tower;
    }
    // Waypoint path with precomputed segment lengths for cheap sampling by travelled distance.
    public sealed class LevelPath
    {
        public readonly Vector2[] Points;
        public readonly float Length;
        readonly float[] start;     // distance at the start of each segment
        readonly Vector2[] dirs;

        public LevelPath(Vector2[] points)
        {
            Points = points;
            int segments = points.Length - 1;
            start = new float[segments + 1];
            dirs = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                var d = points[i + 1] - points[i];
                dirs[i] = d.normalized;
                start[i + 1] = start[i] + d.magnitude;
            }
            Length = start[segments];
        }

        public int SegmentCount => dirs.Length;

        public Vector2 Sample(float distance, ref int segment, out Vector2 dir)
        {
            while (segment < dirs.Length - 1 && distance > start[segment + 1]) segment++;
            dir = dirs[segment];
            return Points[segment] + dir * (distance - start[segment]);
        }

        public float DistanceTo(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < dirs.Length; i++)
            {
                var a = Points[i];
                var ab = Points[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best;
        }
    }

    // Fixed set of objects created once; Get/Release only toggle them.
    // Active items are packed at the front of Active for cheap iteration and O(1) removal.
    public sealed class Pool<T> where T : class
    {
        public readonly List<T> Active;
        readonly Stack<T> free;
        readonly System.Func<T> create;
        readonly System.Action<T, bool> setVisible;
        readonly System.Func<T, int> getIndex;
        readonly System.Action<T, int> setIndex;

        public Pool(int prewarm, System.Func<T> create, System.Action<T, bool> setVisible,
                    System.Func<T, int> getIndex, System.Action<T, int> setIndex)
        {
            this.create = create;
            this.setVisible = setVisible;
            this.getIndex = getIndex;
            this.setIndex = setIndex;
            Active = new List<T>(prewarm);
            free = new Stack<T>(prewarm);
            for (int i = 0; i < prewarm; i++)
            {
                var item = create();
                setVisible(item, false);
                free.Push(item);
            }
        }

        // Objects created after prewarm; should stay 0 during normal play.
        public int Created { get; private set; }

        public T Get()
        {
            T item;
            if (free.Count > 0) item = free.Pop();
            else { item = create(); Created++; }
            setIndex(item, Active.Count);
            Active.Add(item);
            setVisible(item, true);
            return item;
        }

        public void Release(T item)
        {
            int i = getIndex(item);
            int last = Active.Count - 1;
            if (i != last)
            {
                var moved = Active[last];
                Active[i] = moved;
                setIndex(moved, i);
            }
            Active.RemoveAt(last);
            setVisible(item, false);
            free.Push(item);
        }

        public void ReleaseAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Release(Active[i]);
        }
    }
}
