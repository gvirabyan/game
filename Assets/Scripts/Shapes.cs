using UnityEngine;

namespace Corehold
{
    // Placeholder sprites generated at runtime. Every sprite is 1x1 world unit; scale the transform to size it.
    public static class Shapes
    {
        static Sprite circle, ring, square;
        static Material spriteMaterial;

        public static Sprite Circle => circle != null ? circle : circle = MakeDisc(128, 0f);
        public static Sprite Ring => ring != null ? ring : ring = MakeDisc(512, 0.985f);
        public static Sprite Square => square != null ? square : square = MakeSquare();

        public static SpriteRenderer CreateRenderer(string name, Sprite sprite, Color color, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            if (spriteMaterial == null) spriteMaterial = r.sharedMaterial;
            else r.sharedMaterial = spriteMaterial;
            return r;
        }

        // Anti-aliased disc; innerFraction > 0 cuts a hole and makes a ring.
        static Sprite MakeDisc(int size, float innerFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(half - d);
                    if (innerFraction > 0f) a *= Mathf.Clamp01(d - half * innerFraction);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
        }
    }
}
