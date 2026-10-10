using UnityEngine;

namespace Corehold
{
    // All placeholder art, drawn once at startup into a single atlas.
    // Unit and turret sprites are 1 world unit tall and face +X; scale them by their configured size.
    public static class Art
    {
        public static Atlas Atlas { get; private set; }

        public static Sprite Soldier, Tank, Drone;
        public static Sprite TowerBase, MgTurret, CannonTurret, AaTurret;
        public static Sprite Spot, Headquarters;
        public static Sprite Bullet, Shell, Missile;
        public static Sprite Glow, Shadow, Disc, Ring, Pixel;
        public static Sprite Tree, Bush, Rock;
        public static Sprite Coin, Heart, Panel;

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

            Soldier = Draw(128, 1f, DrawSoldier, "Soldier");
            Tank = Draw(128, 1f, DrawTank, "Tank");
            Drone = Draw(128, 1f, DrawDrone, "Drone");
            TowerBase = Draw(128, 1f, DrawTowerBase, "TowerBase");
            MgTurret = Draw(128, 1f, DrawMgTurret, "MgTurret");
            CannonTurret = Draw(128, 1f, DrawCannonTurret, "CannonTurret");
            AaTurret = Draw(128, 1f, DrawAaTurret, "AaTurret");
            Spot = Draw(128, 1f, DrawSpot, "Spot");
            Headquarters = Draw(256, 1f, DrawHeadquarters, "Headquarters");
            Bullet = Draw(32, 1f, p => p.Capsule(-0.55f, 0f, 0.55f, 0f, 0.32f, Hex("FFE27A")), "Bullet");
            Shell = Draw(64, 1f, DrawShell, "Shell");
            Missile = Draw(64, 1f, DrawMissile, "Missile");
            Glow = Draw(128, 1f, p => p.Circle(0f, 0f, 0.55f, White, 0.9f), "Glow");
            Shadow = Draw(64, 1f, p => p.Circle(0f, 0f, 0.6f, new Color(0f, 0f, 0f, 0.45f), 0.7f), "Shadow");
            Disc = Draw(128, 1f, p => p.Circle(0f, 0f, 0.97f, White), "Disc");
            Ring = Draw(256, 1f, p => p.Ring(0f, 0f, 0.975f, 0.025f, White), "Ring");
            Pixel = Draw(16, 1f, p => p.Box(0f, 0f, 1f, 1f, 0f, White), "Pixel", inset: 4);
            Tree = Draw(128, 1f, DrawTree, "Tree");
            Bush = Draw(64, 1f, DrawBush, "Bush");
            Rock = Draw(64, 1f, DrawRock, "Rock");
            Coin = Draw(64, 1f, DrawCoin, "Coin");
            Heart = Draw(64, 1f, DrawHeart, "Heart");
            Panel = Draw(64, 0.64f, p => p.Box(0f, 0f, 1f, 1f, 0.6f, White), "Panel", border: new Vector4(24, 24, 24, 24));

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

        // ---------------- Enemies (desert faction) ----------------

        static void DrawSoldier(Painter p)
        {
            p.Circle(0.06f, -0.08f, 0.62f, Alpha(Black, 0.3f), 0.3f);
            p.Box(-0.42f, 0f, 0.2f, 0.36f, 0.1f, Hex("6E5B36"));                 // backpack
            p.Box(0f, 0f, 0.3f, 0.6f, 0.28f, Hex("A08A5A"));                      // shoulders
            p.Capsule(0.05f, 0.42f, 0.48f, 0.2f, 0.12f, Hex("947E50"));           // arms
            p.Capsule(0.05f, -0.42f, 0.42f, -0.18f, 0.12f, Hex("947E50"));
            p.Capsule(0.15f, -0.12f, 0.95f, -0.12f, 0.07f, Hex("262626"));        // rifle
            p.Circle(0f, 0f, 0.34f, Hex("7E6A40"));                               // helmet
            p.Circle(-0.08f, 0.09f, 0.16f, Alpha(Hex("B39C68"), 0.7f), 0.12f);
        }

        static void DrawTank(Painter p)
        {
            p.Box(0.06f, -0.06f, 0.95f, 0.8f, 0.2f, Alpha(Black, 0.3f), 0f, 0.15f);
            for (int side = -1; side <= 1; side += 2)
            {
                p.Box(0f, 0.62f * side, 0.94f, 0.22f, 0.1f, Hex("2A2A2A"));         // tracks
                for (int i = -4; i <= 4; i++)
                    p.Box(i * 0.2f, 0.62f * side, 0.035f, 0.19f, 0f, Hex("454545"));
            }
            p.Box(0f, 0f, 0.82f, 0.5f, 0.14f, Hex("9C8458"));                     // hull
            p.Box(-0.08f, 0f, 0.58f, 0.36f, 0.1f, Hex("B09868"));
            p.Box(0.6f, 0f, 0.4f, 0.075f, 0.03f, Hex("4A3E28"));                  // barrel
            p.Box(0.95f, 0f, 0.05f, 0.11f, 0.02f, Hex("2E2618"));                 // muzzle
            p.Circle(-0.06f, 0f, 0.36f, Hex("7E6A44"));                           // turret
            p.Circle(-0.18f, 0.13f, 0.1f, Hex("5E4F33"));                         // hatch
        }

        static void DrawDrone(Painter p)
        {
            var arm = Hex("3B3F45");
            p.Capsule(-0.58f, -0.58f, 0.58f, 0.58f, 0.07f, arm);
            p.Capsule(-0.58f, 0.58f, 0.58f, -0.58f, 0.07f, arm);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                p.Circle(0.58f * sx, 0.58f * sy, 0.34f, Alpha(Hex("E6EEF5"), 0.25f));
                p.Ring(0.58f * sx, 0.58f * sy, 0.34f, 0.04f, Alpha(Hex("E6EEF5"), 0.6f));
                p.Circle(0.58f * sx, 0.58f * sy, 0.08f, Hex("1E1E1E"));
            }
            p.Box(0f, 0f, 0.32f, 0.24f, 0.11f, Hex("C44536"));
            p.Box(-0.05f, 0f, 0.16f, 0.1f, 0.05f, Hex("8E2F24"));
            p.Circle(0.3f, 0f, 0.07f, Hex("7FDBFF"));
        }

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

        static void DrawSpot(Painter p)
        {
            p.Box(0f, 0f, 0.86f, 0.86f, 0.28f, Alpha(White, 0.14f));
            p.Box(0f, 0f, 0.72f, 0.72f, 0.22f, Alpha(Black, 0.08f));
            p.Box(0f, 0f, 0.3f, 0.065f, 0.03f, Alpha(White, 0.6f));
            p.Box(0f, 0f, 0.065f, 0.3f, 0.03f, Alpha(White, 0.6f));
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

        // ---------------- Projectiles ----------------

        static void DrawShell(Painter p)
        {
            p.Circle(0f, 0f, 0.8f, Alpha(Palette.Explosion, 0.35f), 0.4f);
            p.Circle(0f, 0f, 0.45f, Hex("2B2B2B"));
            p.Circle(-0.12f, 0.12f, 0.16f, Hex("6A6A6A"));
        }

        static void DrawMissile(Painter p)
        {
            p.Capsule(-0.95f, 0f, -0.6f, 0f, 0.14f, Alpha(Palette.Explosion, 0.8f), 0.1f);
            p.Box(-0.5f, 0.24f, 0.12f, 0.08f, 0.03f, Hex("9AA5B1"));
            p.Box(-0.5f, -0.24f, 0.12f, 0.08f, 0.03f, Hex("9AA5B1"));
            p.Capsule(-0.6f, 0f, 0.55f, 0f, 0.18f, Hex("EDEDED"));
            p.Circle(0.6f, 0f, 0.17f, Palette.Danger);
        }

        // ---------------- Decor and UI ----------------

        static void DrawTree(Painter p)
        {
            p.Circle(0.12f, -0.12f, 0.85f, Alpha(Black, 0.3f), 0.25f);
            p.Circle(0f, 0f, 0.82f, Hex("2F4F24"));
            p.Circle(-0.15f, 0.15f, 0.6f, Hex("3E6630"));
            p.Circle(-0.28f, 0.28f, 0.3f, Hex("4F7D3D"));
        }

        static void DrawBush(Painter p)
        {
            p.Circle(0.1f, -0.1f, 0.75f, Alpha(Black, 0.25f), 0.25f);
            p.Circle(-0.3f, 0f, 0.45f, Hex("3E6630"));
            p.Circle(0.3f, 0.05f, 0.42f, Hex("365A2A"));
            p.Circle(0f, 0.3f, 0.4f, Hex("4A7838"));
        }

        static void DrawRock(Painter p)
        {
            p.Box(0.08f, -0.1f, 0.7f, 0.48f, 0.3f, Alpha(Black, 0.3f), 15f, 0.15f);
            p.Box(0f, 0f, 0.68f, 0.46f, 0.3f, Hex("7D7F7A"), 15f);
            p.Box(-0.1f, 0.1f, 0.4f, 0.22f, 0.2f, Hex("989A94"), 15f);
        }

        static void DrawCoin(Painter p)
        {
            p.Circle(0f, 0f, 0.92f, Hex("D9A53A"));
            p.Circle(0f, 0.04f, 0.78f, Palette.Gold);
            p.Circle(0f, 0.04f, 0.55f, Hex("F7D774"));
            p.Box(0f, 0.04f, 0.12f, 0.36f, 0.05f, Hex("D9A53A"));
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
