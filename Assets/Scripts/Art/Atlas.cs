using System;
using UnityEngine;

namespace Corehold
{
    // One runtime texture that every generated sprite lives in, so the whole game shares
    // a single texture and material and batches into very few draw calls.
    public sealed class Atlas
    {
        const int Pad = 4;

        public readonly Texture2D Texture;
        readonly int size;
        readonly Color[] pixels;
        int cursorX = Pad, cursorY = Pad, rowHeight;

        public Atlas(int size)
        {
            this.size = size;
            pixels = new Color[size * size];
            Texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "GeneratedAtlas",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public Painter Allocate(int width, int height)
        {
            if (cursorX + width + Pad > size)
            {
                cursorX = Pad;
                cursorY += rowHeight + Pad;
                rowHeight = 0;
            }
            if (cursorY + height + Pad > size)
                throw new InvalidOperationException("Generated atlas is full; increase its size");
            var painter = new Painter(this, cursorX, cursorY, width, height);
            cursorX += width + Pad;
            rowHeight = Mathf.Max(rowHeight, height);
            return painter;
        }

        public void Apply()
        {
            Texture.SetPixels(pixels);
            Texture.Apply(true, true);
        }

        internal void Blend(int x, int y, Color c, float coverage)
        {
            int i = y * size + x;
            var dst = pixels[i];
            float sa = c.a * coverage;
            if (sa <= 0f)
            {
                // Bleed colour into fully transparent neighbours so bilinear filtering has no dark fringe.
                if (dst.a <= 0f) pixels[i] = new Color(c.r, c.g, c.b, 0f);
                return;
            }
            float outA = sa + dst.a * (1f - sa);
            float k = dst.a * (1f - sa);
            pixels[i] = new Color(
                (c.r * sa + dst.r * k) / outA,
                (c.g * sa + dst.g * k) / outA,
                (c.b * sa + dst.b * k) / outA,
                outA);
        }
    }

    // Draws anti-aliased shapes into one atlas region using signed distance functions.
    // Coordinates are normalized: the region's centre is (0,0) and its half-height is 1.
    // Unit sprites face +X so they can be rotated toward their movement direction.
    public readonly struct Painter
    {
        readonly Atlas atlas;
        public readonly int X, Y, W, H;

        public Painter(Atlas atlas, int x, int y, int w, int h)
        {
            this.atlas = atlas;
            X = x; Y = y; W = w; H = h;
        }

        float Unit => H * 0.5f;

        public void Circle(float x, float y, float r, Color c, float soft = 0f)
        {
            var center = new Vector2(x, y);
            Fill(x - r, y - r, x + r, y + r, soft, c, p => (p - center).magnitude - r);
        }

        public void Ring(float x, float y, float r, float thickness, Color c)
        {
            var center = new Vector2(x, y);
            float half = thickness * 0.5f;
            Fill(x - r - half, y - r - half, x + r + half, y + r + half, 0f, c,
                p => Mathf.Abs((p - center).magnitude - r) - half);
        }

        // Rounded rectangle with half extents (hw, hh), corner radius and optional rotation in degrees.
        public void Box(float x, float y, float hw, float hh, float radius, Color c, float angle = 0f, float soft = 0f)
        {
            var center = new Vector2(x, y);
            float rad = -angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float reach = Mathf.Sqrt(hw * hw + hh * hh);
            radius = Mathf.Min(radius, Mathf.Min(hw, hh));
            Fill(x - reach, y - reach, x + reach, y + reach, soft, c, p =>
            {
                var d = p - center;
                var q = new Vector2(Mathf.Abs(d.x * cos - d.y * sin), Mathf.Abs(d.x * sin + d.y * cos));
                var e = new Vector2(q.x - hw + radius, q.y - hh + radius);
                float outside = new Vector2(Mathf.Max(e.x, 0f), Mathf.Max(e.y, 0f)).magnitude;
                return outside + Mathf.Min(Mathf.Max(e.x, e.y), 0f) - radius;
            });
        }

        public void Capsule(float ax, float ay, float bx, float by, float r, Color c, float soft = 0f)
        {
            var a = new Vector2(ax, ay);
            var ab = new Vector2(bx - ax, by - ay);
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            Fill(Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r, soft, c, p =>
            {
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                return (p - (a + ab * t)).magnitude - r;
            });
        }

        void Fill(float minX, float minY, float maxX, float maxY, float soft, Color c, Func<Vector2, float> sdf)
        {
            float unit = Unit;
            float edge = Mathf.Max(soft, 1f / unit);
            float margin = edge + 2f / unit;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - margin) * unit + W * 0.5f));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt((maxX + margin) * unit + W * 0.5f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((minY - margin) * unit + H * 0.5f));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt((maxY + margin) * unit + H * 0.5f));
            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    var p = new Vector2((px + 0.5f - W * 0.5f) / unit, (py + 0.5f - H * 0.5f) / unit);
                    float coverage = Mathf.Clamp01(0.5f - sdf(p) / edge);
                    atlas.Blend(X + px, Y + py, c, coverage);
                }
            }
        }

        // worldHeight: size of the sprite in world units. inset trims pixels off each side (for stretched sprites).
        public Sprite ToSprite(string name, float worldHeight, int inset = 0, Vector4 border = default)
        {
            var rect = new Rect(X + inset, Y + inset, W - inset * 2, H - inset * 2);
            var sprite = Sprite.Create(atlas.Texture, rect, new Vector2(0.5f, 0.5f), rect.height / worldHeight,
                0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }
    }
}
