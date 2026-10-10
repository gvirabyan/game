using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Enemies and projectiles are plain objects driven by Game.Update, not MonoBehaviours,
    // so 200+ of them cost one Update call instead of hundreds.

    public sealed class Enemy
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 position;
        public Vector2 direction;           // unit vector toward the tower
        public float health, maxHealth, speed, damagePerSecond, reward;
        public bool atTower;
        public int generation;              // bumps on every release so stale references can be detected
        public int activeIndex;
    }

    public sealed class Projectile
    {
        public Transform transform;
        public Vector2 position;
        public Enemy target;
        public int targetGeneration;
        public float damage;
        public int activeIndex;
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
