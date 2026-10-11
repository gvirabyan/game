using UnityEngine;

namespace Corehold
{
    // All placeholder art, drawn once at startup into a single atlas.
    // Unit and turret sprites are 1 world unit tall and face +X; scale them by their configured size.
    public static class Art
    {
        public static Atlas Atlas { get; private set; }

        // UI icons (the world itself is 3D, see Models).
        public static Sprite TowerBase, MgTurret, CannonTurret, AaTurret, Headquarters;
        public static Sprite Coin, Heart, Flag, Pause, Play, FastForward, Close;
        public static Sprite Panel, PanelShadow, ButtonFace, Vignette, Star, Pill, Header, Fade, RangeDecal, PadMarker, SoundOn, SoundOff;
        // World helpers: billboards, range indicator, health bars.
        public static Sprite Glow, Disc, Ring, Pixel;

        public static class Palette
        {
            public static readonly Color Ground = Hex("4C6B3C");
            public static readonly Color Road = Hex("C9B68C");
            public static readonly Color RoadEdge = Hex("A28E62");
            public static readonly Color Accent = Hex("2F80ED");
            public static readonly Color Gold = Hex("F2C14E");
            public static readonly Color Danger = Hex("E5484D");
            public static readonly Color Good = Hex("3DDC97");
            public static readonly Color Explosion = Hex("FF9A3C");
        }

        static readonly Color Black = Color.black;
        static readonly Color White = Color.white;

        public static void Build()
        {
            if (Atlas != null) return;
            Atlas = new Atlas(1024);

            TowerBase = Draw(128, 1f, DrawTowerBase, "TowerBase");
            MgTurret = Draw(128, 1f, DrawMgTurret, "MgTurret");
            CannonTurret = Draw(128, 1f, DrawCannonTurret, "CannonTurret");
            AaTurret = Draw(128, 1f, DrawAaTurret, "AaTurret");
            Headquarters = Draw(256, 1f, DrawHeadquarters, "Headquarters");
            Glow = Draw(128, 1f, p => p.Circle(0f, 0f, 0.55f, White, 0.9f), "Glow");
            Disc = Draw(128, 1f, p => p.Circle(0f, 0f, 0.97f, White), "Disc");
            Ring = Draw(256, 1f, p => p.Ring(0f, 0f, 0.96f, 0.04f, White), "Ring");
            RangeDecal = Draw(256, 1f, DrawRangeDecal, "RangeDecal");
            SoundOn = Draw(64, 1f, p => DrawSpeaker(p, true), "SoundOn");
            SoundOff = Draw(64, 1f, p => DrawSpeaker(p, false), "SoundOff");
            PadMarker = Draw(128, 1f, DrawPadMarker, "PadMarker");
            Pixel = Draw(16, 1f, p => p.Box(0f, 0f, 1f, 1f, 0f, White), "Pixel", inset: 4);
            Coin = Draw(64, 1f, DrawCoin, "Coin");
            Heart = Draw(64, 1f, DrawHeart, "Heart");
            Flag = Draw(64, 1f, DrawFlag, "Flag");
            Pause = Draw(64, 1f, p => { p.Box(-0.32f, 0f, 0.15f, 0.62f, 0.08f, White); p.Box(0.32f, 0f, 0.15f, 0.62f, 0.08f, White); }, "Pause");
            Play = Draw(64, 1f, p => p.Polygon(new[] { new Vector2(-0.45f, 0.65f), new Vector2(0.7f, 0f), new Vector2(-0.45f, -0.65f) }, White), "Play");
            FastForward = Draw(64, 1f, DrawFastForward, "FastForward");
            Close = Draw(64, 1f, p => { p.Box(0f, 0f, 0.62f, 0.11f, 0.08f, White, 45f); p.Box(0f, 0f, 0.62f, 0.11f, 0.08f, White, -45f); }, "Close");
            // Rounded panel (sliced), its soft drop shadow, and a button face with a darker bottom lip.
            // Panels are tinted by Image.color: a bright rim around a slightly darker body.
            Panel = Draw(64, 0.64f, p => { p.Box(0f, 0f, 1f, 1f, 0.75f, White); p.Box(0f, 0f, 0.93f, 0.93f, 0.68f, Grey(0.78f)); },
                "Panel", border: new Vector4(26, 26, 26, 26));
            // Panel title strip: rounded top corners only.
            Header = Draw(64, 0.64f, p => { p.Box(0f, 0f, 1f, 1f, 0.75f, White); p.Box(0f, -0.6f, 1f, 0.4f, 0f, White); },
                "Header", border: new Vector4(26, 26, 26, 26));
            Pill = Draw(32, 0.32f, p => p.Box(0f, 0f, 1f, 1f, 1f, White), "Pill", border: new Vector4(15, 15, 15, 15));
            Star = Draw(64, 1f, DrawStar, "Star");
            // Opaque on the left, fading out to the right.
            Fade = Draw(64, 1f, p => p.Box(-0.9f, 0f, 1.1f, 1.5f, 0f, White, 0f, 0.9f), "Fade");
            PanelShadow = Draw(96, 0.96f, p => p.Box(0f, 0f, 0.8f, 0.8f, 0.5f, new Color(0f, 0f, 0f, 0.5f), 0f, 0.3f), "PanelShadow", border: new Vector4(46, 46, 46, 46));
            ButtonFace = Draw(64, 0.64f, DrawButtonFace, "ButtonFace", border: new Vector4(26, 26, 26, 26));

            Vignette = Draw(64, 1f, p => p.Outside(0.55f, new Color(0f, 0f, 0f, 0.55f), 0.9f), "Vignette");

            Atlas.Apply();
        }

        static Sprite Draw(int px, float worldHeight, System.Action<Painter> draw, string name, int inset = 0, Vector4 border = default)
        {
            var painter = Atlas.Allocate(px, px);
            draw(painter);
            return painter.ToSprite(name, worldHeight, inset, border);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------- Player towers (steel blue) ----------------

        static void DrawTowerBase(Painter p)
        {
            p.Box(0.05f, -0.07f, 0.9f, 0.9f, 0.26f, Alpha(Black, 0.35f), 0f, 0.12f);
            p.Box(0f, 0f, 0.88f, 0.88f, 0.24f, Hex("2B3645"));
            p.Box(0f, 0f, 0.76f, 0.76f, 0.2f, Hex("3E4C5E"));
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                p.Circle(0.58f * sx, 0.58f * sy, 0.06f, Hex("6B7D94"));
        }

        static void DrawMgTurret(Painter p)
        {
            p.Circle(0.04f, -0.05f, 0.44f, Alpha(Black, 0.3f), 0.1f);
            p.Capsule(0.15f, 0.13f, 0.95f, 0.13f, 0.065f, Hex("1F262E"));
            p.Capsule(0.15f, -0.13f, 0.95f, -0.13f, 0.065f, Hex("1F262E"));
            p.Circle(0f, 0f, 0.42f, Hex("5F7D9C"));
            p.Circle(-0.04f, 0.04f, 0.3f, Hex("7596B8"));
            p.Box(0.32f, 0f, 0.1f, 0.24f, 0.05f, Hex("4A6480"));
        }

        static void DrawCannonTurret(Painter p)
        {
            p.Box(0f, -0.05f, 0.48f, 0.45f, 0.2f, Alpha(Black, 0.3f), 0f, 0.1f);
            p.Box(0.55f, 0f, 0.42f, 0.13f, 0.04f, Hex("1F262E"));
            p.Box(0.95f, 0f, 0.06f, 0.18f, 0.03f, Hex("11161B"));
            p.Box(-0.05f, 0f, 0.46f, 0.42f, 0.18f, Hex("5F7D9C"));
            p.Box(-0.1f, 0.03f, 0.3f, 0.28f, 0.12f, Hex("7596B8"));
        }

        static void DrawAaTurret(Painter p)
        {
            p.Box(0f, -0.05f, 0.46f, 0.55f, 0.16f, Alpha(Black, 0.3f), 0f, 0.1f);
            p.Box(-0.1f, 0f, 0.42f, 0.55f, 0.16f, Hex("5F7D9C"));
            for (int i = -1; i <= 1; i += 2)
            {
                p.Box(0.28f, 0.24f * i, 0.5f, 0.14f, 0.07f, Hex("E8ECEF"));
                p.Circle(0.74f, 0.24f * i, 0.13f, Palette.Danger);
            }
            p.Box(-0.25f, 0f, 0.16f, 0.4f, 0.08f, Hex("4A6480"));
        }

        static void DrawHeadquarters(Painter p)
        {
            var yellow = Palette.Gold;
            p.Box(0.05f, -0.06f, 0.92f, 0.92f, 0.22f, Alpha(Black, 0.35f), 0f, 0.1f);
            p.Box(0f, 0f, 0.9f, 0.9f, 0.2f, Hex("2E3A48"));
            p.Box(0f, 0f, 0.76f, 0.76f, 0.15f, Hex("465870"));
            p.Circle(0f, 0f, 0.5f, Hex("34445A"));
            p.Ring(0f, 0f, 0.5f, 0.05f, yellow);
            p.Box(-0.17f, 0f, 0.05f, 0.26f, 0.02f, yellow);
            p.Box(0.17f, 0f, 0.05f, 0.26f, 0.02f, yellow);
            p.Box(0f, 0f, 0.17f, 0.05f, 0.02f, yellow);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                p.Circle(0.66f * sx, 0.66f * sy, 0.06f, Palette.Good);
        }

        static void DrawCoin(Painter p)
        {
            p.Circle(0f, 0f, 0.92f, Hex("D9A53A"));
            p.Circle(0f, 0.04f, 0.78f, Palette.Gold);
            p.Circle(0f, 0.04f, 0.55f, Hex("F7D774"));
            p.Box(0f, 0.04f, 0.12f, 0.36f, 0.05f, Hex("D9A53A"));
        }

        static void DrawFlag(Painter p)
        {
            p.Box(-0.55f, 0f, 0.07f, 0.85f, 0.04f, White);
            p.Polygon(new[] { new Vector2(-0.5f, 0.8f), new Vector2(0.75f, 0.45f), new Vector2(-0.5f, 0.1f) }, White);
        }

        static void DrawFastForward(Painter p)
        {
            p.Polygon(new[] { new Vector2(-0.8f, 0.55f), new Vector2(0f, 0f), new Vector2(-0.8f, -0.55f) }, White);
            p.Polygon(new[] { new Vector2(0f, 0.55f), new Vector2(0.8f, 0f), new Vector2(0f, -0.55f) }, White);
        }

        // Chunky game button (tinted by Image.color): dark bottom lip, face, and a lighter glossy top half.
        static void DrawButtonFace(Painter p)
        {
            p.Box(0f, 0f, 1f, 1f, 0.75f, Grey(0.5f));
            p.Box(0f, 0.1f, 1f, 0.9f, 0.75f, Grey(0.86f));
            p.Box(0f, 0.42f, 0.9f, 0.5f, 0.6f, White, 0f, 0.25f);
            p.Box(0f, 0.72f, 0.7f, 0.07f, 0.07f, new Color(1f, 1f, 1f, 0.6f), 0f, 0.05f);
        }

        static Color Grey(float v) => new Color(v, v, v, 1f);

        // Ground decal for a tower's reach: faint fill that brightens toward a solid rim, plus a dashed inner ring
        // (the decal is rotated slowly so the dashes travel).
        static void DrawRangeDecal(Painter p)
        {
            p.Circle(0f, 0f, 0.95f, new Color(1f, 1f, 1f, 0.07f));
            for (int i = 0; i < 6; i++)
                p.Ring(0f, 0f, 0.62f + i * 0.06f, 0.06f, new Color(1f, 1f, 1f, 0.025f + i * 0.02f));
            p.Ring(0f, 0f, 0.955f, 0.035f, White);
            const int dashes = 36;
            for (int i = 0; i < dashes; i++)
            {
                float a = i * 360f / dashes;
                float r = a * Mathf.Deg2Rad;
                p.Box(Mathf.Cos(r) * 0.875f, Mathf.Sin(r) * 0.875f, 0.012f, 0.04f, 0.01f, new Color(1f, 1f, 1f, 0.75f), a);
            }
        }

        static void DrawSpeaker(Painter p, bool on)
        {
            p.Box(-0.52f, 0f, 0.14f, 0.22f, 0.04f, White);
            p.Polygon(new[] { new Vector2(-0.42f, 0.22f), new Vector2(-0.02f, 0.6f), new Vector2(-0.02f, -0.6f), new Vector2(-0.42f, -0.22f) }, White);
            if (on)
            {
                p.Capsule(0.22f, 0.24f, 0.22f, -0.24f, 0.07f, White);
                p.Capsule(0.48f, 0.44f, 0.48f, -0.44f, 0.07f, White);
            }
            else
            {
                p.Box(0.36f, 0f, 0.3f, 0.07f, 0.05f, White, 45f);
                p.Box(0.36f, 0f, 0.3f, 0.07f, 0.05f, White, -45f);
            }
        }

        // Selection marker for an empty pad: a ring with four bracket ticks.
        static void DrawPadMarker(Painter p)
        {
            p.Circle(0f, 0f, 0.86f, new Color(1f, 1f, 1f, 0.16f));
            p.Ring(0f, 0f, 0.86f, 0.07f, White);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f);
                float r = a * Mathf.Deg2Rad;
                p.Box(Mathf.Cos(r) * 0.95f, Mathf.Sin(r) * 0.95f, 0.035f, 0.1f, 0.03f, White, a + 90f);
            }
        }

        // Five-pointed star from convex pieces: a pentagon and five triangles.
        static void DrawStar(Painter p)
        {
            var outer = new Vector2[5];
            var inner = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad, b = (126f + i * 72f) * Mathf.Deg2Rad;
                outer[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.95f;
                inner[i] = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.4f;
            }
            p.Polygon(inner, White);
            for (int i = 0; i < 5; i++)
                p.Polygon(new[] { outer[i], inner[i], inner[(i + 4) % 5] }, White);
        }

        static void DrawHeart(Painter p)
        {
            var c = Palette.Danger;
            p.Circle(-0.36f, 0.28f, 0.44f, c);
            p.Circle(0.36f, 0.28f, 0.44f, c);
            p.Box(0f, -0.12f, 0.52f, 0.52f, 0.1f, c, 45f);
        }
    }
}
