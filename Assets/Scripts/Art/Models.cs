using UnityEngine;

namespace Corehold
{
    // Low-poly 3D models built once at startup. All share one vertex-colour material (Corehold/LowPoly)
    // so GPU instancing batches every copy of a mesh. Unit models face +X and have a footprint of about
    // 1 world unit; scale them by their configured size.
    public static class Models
    {
        public static Material Material { get; private set; }

        public static Mesh Soldier, Tank, Drone;
        public static Mesh TowerBase, MgTurret, CannonTurret, AaTurret, Pip;
        public static Mesh Bullet, Shell, Missile;

        // Colours. Alpha below 1 makes a part glow (see LowPoly.shader).
        static readonly Color Sand = Art.Hex("A68F5E"), SandLight = Art.Hex("BFA673"), SandDark = Art.Hex("7A6640");
        static readonly Color Track = Art.Hex("2B2B2B"), Gun = Art.Hex("2A2F35"), Skin = Art.Hex("D9A97E");
        static readonly Color Steel = Art.Hex("5F7D9C"), SteelLight = Art.Hex("7FA0C2"), SteelDark = Art.Hex("3B4C61");
        static readonly Color Concrete = Art.Hex("8E959C"), ConcreteLight = Art.Hex("A9B0B8");
        static readonly Color Leaf = Art.Hex("4C8A38"), LeafLight = Art.Hex("6EA84C"), Pine = Art.Hex("2E6A40"), Bark = Art.Hex("7A5434");
        static readonly Color Stone = Art.Hex("9A9A92");
        static readonly Color Red = Art.Hex("D64541"), White = Art.Hex("EDEFF2");

        static Color Glow(Color c, float amount = 1f) => new Color(c.r, c.g, c.b, 1f - amount);

        public static void Build()
        {
            if (Material != null) return;
            var shader = Shader.Find("Corehold/LowPoly");
            if (shader == null) throw new System.InvalidOperationException("Shader Corehold/LowPoly missing from Resources/Shaders");
            Material = new Material(shader) { name = "LowPoly", enableInstancing = true };

            Soldier = Make("Soldier", BuildSoldier);
            Tank = Make("Tank", BuildTank);
            Drone = Make("Drone", BuildDrone);
            TowerBase = Make("TowerBase", BuildTowerBase);
            MgTurret = Make("MgTurret", BuildMgTurret);
            CannonTurret = Make("CannonTurret", BuildCannonTurret);
            AaTurret = Make("AaTurret", BuildAaTurret);
            Pip = Make("Pip", b => b.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.09f, 0.06f, 0.09f), Glow(Art.Palette.Gold, 0.7f)));
            Bullet = Make("Bullet", b => b.Box(Vector3.zero, new Vector3(0.32f, 0.05f, 0.05f), Glow(Art.Hex("FFE27A"))));
            Shell = Make("Shell", b => b.Sphere(Vector3.zero, Vector3.one * 0.09f, Art.Hex("2E2E2E"), 3, 6));
            Missile = Make("Missile", BuildMissile);
        }

        static Mesh Make(string name, System.Action<MeshBuilder> build)
        {
            var b = new MeshBuilder();
            build(b);
            return b.ToMesh(name);
        }

        // ---------------- Enemies (desert faction) ----------------

        static void BuildSoldier(MeshBuilder b)
        {
            var pants = SandDark;
            b.Box(new Vector3(0f, 0.25f, 0.14f), new Vector3(0.24f, 0.5f, 0.18f), pants);
            b.Box(new Vector3(0f, 0.25f, -0.14f), new Vector3(0.24f, 0.5f, 0.18f), pants);
            b.Box(new Vector3(0f, 0.78f, 0f), new Vector3(0.34f, 0.56f, 0.6f), Sand, 0f, false, SandLight);
            b.Box(new Vector3(-0.26f, 0.82f, 0f), new Vector3(0.2f, 0.42f, 0.44f), Art.Hex("6E5B36"));
            b.Box(new Vector3(0.16f, 0.82f, 0.34f), new Vector3(0.42f, 0.15f, 0.15f), Sand);
            b.Box(new Vector3(0.16f, 0.82f, -0.34f), new Vector3(0.42f, 0.15f, 0.15f), Sand);
            b.Box(new Vector3(0.36f, 0.86f, -0.2f), new Vector3(0.66f, 0.08f, 0.08f), Gun);
            b.Sphere(new Vector3(0.03f, 1.16f, 0f), Vector3.one * 0.19f, Skin, 3, 6);
            b.Sphere(new Vector3(0f, 1.24f, 0f), new Vector3(0.25f, 0.15f, 0.25f), Art.Hex("6F5D38"), 3, 7);
        }

        static void BuildTank(MeshBuilder b)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(0f, 0.14f, 0.36f * side), new Vector3(1.02f, 0.28f, 0.22f), Track);
                for (int i = -2; i <= 2; i++)
                    b.Box(new Vector3(i * 0.2f, 0.12f, 0.475f * side), new Vector3(0.14f, 0.14f, 0.01f), Art.Hex("4A4A4A"));
            }
            b.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.92f, 0.22f, 0.6f), Sand, 0f, false, SandLight);
            b.Box(new Vector3(0.42f, 0.3f, 0f), new Vector3(0.12f, 0.16f, 0.56f), SandDark);
            b.Cylinder(new Vector3(-0.06f, 0.45f, 0f), 0.26f, 0.17f, 8, Art.Hex("8E7A4E"), Art.Hex("A38D5D"));
            b.Barrel(new Vector3(0.14f, 0.54f, 0f), 0.62f, 0.045f, 6, Art.Hex("4A3E28"));
            b.Barrel(new Vector3(0.7f, 0.54f, 0f), 0.08f, 0.065f, 6, Art.Hex("3A3020"));
            b.Cylinder(new Vector3(-0.14f, 0.62f, 0.1f), 0.08f, 0.04f, 6, SandDark);
        }

        static void BuildDrone(MeshBuilder b)
        {
            b.Box(Vector3.zero, new Vector3(1.15f, 0.05f, 0.07f), Gun, 45f);
            b.Box(Vector3.zero, new Vector3(1.15f, 0.05f, 0.07f), Gun, -45f);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var c = new Vector3(0.4f * sx, 0.03f, 0.4f * sz);
                b.Cylinder(c, 0.05f, 0.06f, 6, Gun);
                b.Cylinder(c + new Vector3(0f, 0.06f, 0f), 0.24f, 0.015f, 10, Art.Hex("C9D3DC"));
            }
            b.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.16f, 0.3f), Red, 0f, false, Art.Hex("E25A4F"));
            b.Box(new Vector3(-0.05f, 0.12f, 0f), new Vector3(0.2f, 0.05f, 0.18f), Art.Hex("8E2F24"));
            b.Sphere(new Vector3(0.22f, -0.02f, 0f), Vector3.one * 0.06f, Glow(Art.Hex("7FDBFF"), 0.8f), 3, 6);
        }

        // ---------------- Player towers (steel blue) ----------------

        static void BuildTowerBase(MeshBuilder b)
        {
            b.Cylinder(Vector3.zero, 0.47f, 0.22f, 8, SteelDark, Art.Hex("47586E"));
            b.Cylinder(new Vector3(0f, 0.22f, 0f), 0.4f, 0.1f, 8, Art.Hex("4F6178"), Art.Hex("5C6F88"));
        }

        static void BuildMgTurret(MeshBuilder b)
        {
            b.Cylinder(Vector3.zero, 0.24f, 0.2f, 8, Steel, SteelLight);
            b.Box(new Vector3(0.16f, 0.12f, 0f), new Vector3(0.14f, 0.14f, 0.24f), SteelDark);
            b.Barrel(new Vector3(0.2f, 0.13f, 0.065f), 0.48f, 0.03f, 6, Gun);
            b.Barrel(new Vector3(0.2f, 0.13f, -0.065f), 0.48f, 0.03f, 6, Gun);
            b.Box(new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.12f, 0.08f, 0.16f), Art.Hex("2D3A4A"));
        }

        static void BuildCannonTurret(MeshBuilder b)
        {
            b.Box(new Vector3(-0.04f, 0.12f, 0f), new Vector3(0.46f, 0.24f, 0.4f), Steel, 0f, false, SteelLight);
            b.Box(new Vector3(-0.1f, 0.27f, 0f), new Vector3(0.24f, 0.07f, 0.26f), SteelDark);
            b.Barrel(new Vector3(0.18f, 0.14f, 0f), 0.55f, 0.07f, 8, Gun);
            b.Barrel(new Vector3(0.68f, 0.14f, 0f), 0.1f, 0.095f, 8, Art.Hex("161A1F"));
        }

        static void BuildAaTurret(MeshBuilder b)
        {
            b.Cylinder(Vector3.zero, 0.22f, 0.12f, 8, SteelDark);
            b.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.4f, 0.24f, 0.44f), Steel, 0f, false, SteelLight);
            for (int row = 0; row < 2; row++)
            for (int col = -1; col <= 1; col += 2)
            {
                var start = new Vector3(0.2f, 0.21f + row * 0.12f, 0.1f * col);
                b.Barrel(start, 0.12f, 0.045f, 6, White);
                b.Barrel(start + new Vector3(0.12f, 0f, 0f), 0.05f, 0.047f, 6, Glow(Red, 0.4f));
            }
        }

        static void BuildMissile(MeshBuilder b)
        {
            b.Barrel(new Vector3(-0.3f, 0f, 0f), 0.1f, 0.035f, 6, Glow(Art.Palette.Explosion));
            b.Barrel(new Vector3(-0.2f, 0f, 0f), 0.3f, 0.045f, 6, White);
            b.Barrel(new Vector3(0.1f, 0f, 0f), 0.08f, 0.047f, 6, Glow(Red, 0.4f));
        }

        // ---------------- Level props, added into the combined static level mesh ----------------

        // Round concrete build pad with a yellow/black hazard ring and a glowing plus.
        public static void Pad(MeshBuilder b, Vector3 at)
        {
            b.Cylinder(at, 0.5f, 0.06f, 16, Art.Hex("6E747A"), Art.Hex("7C838A"));
            b.Cylinder(at + new Vector3(0f, 0.06f, 0f), 0.46f, 0.05f, 16, Concrete, ConcreteLight);
            const int stripes = 16;
            for (int i = 0; i < stripes; i++)
            {
                float a0 = i * Mathf.PI * 2f / stripes, a1 = (i + 1) * Mathf.PI * 2f / stripes;
                var c = i % 2 == 0 ? Art.Hex("F2C14E") : Art.Hex("2B2E33");
                Vector3 P(float a, float r) => at + new Vector3(Mathf.Cos(a) * r, 0.112f, Mathf.Sin(a) * r);
                b.Quad(P(a0, 0.36f), P(a1, 0.36f), P(a1, 0.44f), P(a0, 0.44f), c);
            }
            b.Box(at + new Vector3(0f, 0.115f, 0f), new Vector3(0.3f, 0.01f, 0.07f), Glow(White, 0.35f));
            b.Box(at + new Vector3(0f, 0.115f, 0f), new Vector3(0.07f, 0.01f, 0.3f), Glow(White, 0.35f));
        }

        // Command bunker: concrete block behind sandbags, radar dish, antenna with a beacon, flag and a helipad.
        public static void Headquarters(MeshBuilder b, Vector3 at)
        {
            b.Cylinder(at, 1.05f, 0.08f, 12, Art.Hex("6B7178"), Art.Hex("7A8189"));
            b.Box(at + new Vector3(0f, 0.42f, 0.12f), new Vector3(1.2f, 0.7f, 0.95f), Art.Hex("5E6E5A"), 0f, false, Art.Hex("6F8069"));
            b.Box(at + new Vector3(0f, 0.8f, 0.12f), new Vector3(1.3f, 0.08f, 1.05f), Art.Hex("4D5B4A"));
            b.Box(at + new Vector3(0.2f, 0.98f, 0.25f), new Vector3(0.5f, 0.3f, 0.45f), Art.Hex("56664F"), 0f, false, Art.Hex("677A60"));
            for (int i = -1; i <= 1; i++)
                b.Box(at + new Vector3(i * 0.36f, 0.52f, -0.36f), new Vector3(0.2f, 0.12f, 0.02f), Glow(Art.Hex("FFD27A"), 0.8f));
            b.Box(at + new Vector3(0f, 0.25f, -0.36f), new Vector3(0.3f, 0.34f, 0.02f), Art.Hex("2E352C"));
            // Sandbag walls on both sides of the door.
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 3; i++)
                        b.Sphere(at + new Vector3(side * (0.42f + i * 0.17f) + (row == 1 ? 0.08f : 0f), 0.13f + row * 0.1f, -0.62f),
                            new Vector3(0.1f, 0.06f, 0.08f), Art.Hex("C9B07A"), 2, 6);
            // Radar dish and antenna.
            b.Cylinder(at + new Vector3(-0.38f, 0.84f, 0.35f), 0.04f, 0.25f, 6, Gun);
            b.Cylinder(at + new Vector3(-0.38f, 1.06f, 0.35f), 0.05f, 0.1f, 10, Art.Hex("B8C0C6"), Art.Hex("D9DEE2"), 0.26f);
            b.Cylinder(at + new Vector3(0.4f, 1.13f, 0.4f), 0.025f, 0.75f, 5, Gun);
            b.Sphere(at + new Vector3(0.4f, 1.9f, 0.4f), Vector3.one * 0.06f, Glow(Red), 3, 6);
            b.Cylinder(at + new Vector3(0.82f, 0.08f, -0.55f), 0.025f, 1.2f, 5, ConcreteLight);
            b.Box(at + new Vector3(0.99f, 1.15f, -0.55f), new Vector3(0.34f, 0.2f, 0.02f), Art.Hex("2F80ED"));
            b.Box(at + new Vector3(0.99f, 1.15f, -0.56f), new Vector3(0.12f, 0.08f, 0.02f), Art.Palette.Gold);
        }

        public static void Tree(MeshBuilder b, Vector3 at, float size, int seed)
        {
            var random = new System.Random(seed);
            var tint = 0.9f + 0.2f * (float)random.NextDouble();
            b.Cylinder(at, 0.07f * size, 0.34f * size, 5, Bark);
            if (random.Next(2) == 0)
            {
                var pine = MeshBuilder.Shade(Pine, tint);
                b.Cylinder(at + new Vector3(0f, 0.2f * size, 0f), 0.46f * size, 0.55f * size, 7, pine, null, 0f);
                b.Cylinder(at + new Vector3(0f, 0.5f * size, 0f), 0.36f * size, 0.5f * size, 7, MeshBuilder.Shade(pine, 1.12f), null, 0f);
                b.Cylinder(at + new Vector3(0f, 0.78f * size, 0f), 0.24f * size, 0.45f * size, 7, MeshBuilder.Shade(pine, 1.25f), null, 0f);
            }
            else
            {
                var leaf = MeshBuilder.Shade(Leaf, tint);
                b.Sphere(at + new Vector3(0f, 0.62f * size, 0f), new Vector3(0.44f, 0.4f, 0.44f) * size, leaf, 4, 7, 0.12f, seed);
                b.Sphere(at + new Vector3(-0.16f * size, 0.86f * size, -0.1f * size), new Vector3(0.28f, 0.26f, 0.28f) * size, MeshBuilder.Shade(LeafLight, tint), 3, 6, 0.1f, seed + 1);
                b.Sphere(at + new Vector3(0.2f * size, 0.78f * size, 0.08f * size), new Vector3(0.24f, 0.22f, 0.24f) * size, MeshBuilder.Shade(LeafLight, tint * 1.08f), 3, 6, 0.1f, seed + 2);
            }
        }

        public static void Bush(MeshBuilder b, Vector3 at, float size, int seed)
        {
            b.Sphere(at + new Vector3(0f, 0.14f * size, 0f), new Vector3(0.34f, 0.24f, 0.3f) * size, Leaf, 3, 7, 0.15f, seed);
            b.Sphere(at + new Vector3(0.15f * size, 0.2f * size, 0.1f * size), new Vector3(0.2f, 0.18f, 0.2f) * size, LeafLight, 3, 6, 0.1f, seed + 1);
        }

        public static void Rock(MeshBuilder b, Vector3 at, float size, int seed)
        {
            b.Sphere(at + new Vector3(0f, 0.06f * size, 0f), new Vector3(0.32f, 0.2f, 0.26f) * size, Stone, 3, 6, 0.25f, seed);
        }

        // A few thin blades of grass.
        public static void GrassTuft(MeshBuilder b, Vector3 at, float size, int seed)
        {
            var random = new System.Random(seed);
            var c = Color.Lerp(Art.Hex("5E9A44"), Art.Hex("9CC963"), (float)random.NextDouble());
            for (int i = 0; i < 3; i++)
            {
                var o = new Vector3(((float)random.NextDouble() - 0.5f) * 0.12f, 0f, ((float)random.NextDouble() - 0.5f) * 0.12f) * size;
                b.Cylinder(at + o, 0.025f * size, (0.12f + 0.1f * (float)random.NextDouble()) * size, 3, c, null, 0f);
            }
        }

        public static void Flower(MeshBuilder b, Vector3 at, int seed)
        {
            var random = new System.Random(seed);
            var colors = new[] { Art.Hex("F5F0E6"), Art.Hex("F2C14E"), Art.Hex("E87EA1"), Art.Hex("9DB7F5") };
            b.Cylinder(at, 0.012f, 0.1f, 3, Art.Hex("4F8A3A"));
            b.Sphere(at + new Vector3(0f, 0.11f, 0f), new Vector3(0.04f, 0.025f, 0.04f), colors[random.Next(colors.Length)], 2, 5);
        }

        // Stack of wooden ammo crates.
        public static void Crates(MeshBuilder b, Vector3 at, int seed)
        {
            var random = new System.Random(seed);
            var wood = Art.Hex("9C7A4A");
            float yaw = (float)random.NextDouble() * 90f;
            var r = Quaternion.Euler(0f, yaw, 0f);
            Crate(b, at + r * new Vector3(-0.13f, 0f, 0f), 0.24f, wood, yaw);
            Crate(b, at + r * new Vector3(0.14f, 0f, 0.04f), 0.22f, MeshBuilder.Shade(wood, 0.92f), yaw + 10f);
            if (random.Next(2) == 0) Crate(b, at + r * new Vector3(-0.08f, 0.24f, 0f), 0.2f, MeshBuilder.Shade(wood, 1.08f), yaw - 8f);
        }

        static void Crate(MeshBuilder b, Vector3 at, float s, Color wood, float yaw)
        {
            b.Box(at + new Vector3(0f, s * 0.5f, 0f), Vector3.one * s, wood, yaw, false, MeshBuilder.Shade(wood, 1.12f));
            b.Box(at + new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 1.02f, s * 0.18f, s * 1.02f), Art.Hex("5E4A2C"), yaw);
        }

        public static void Barrels(MeshBuilder b, Vector3 at, int seed)
        {
            var random = new System.Random(seed);
            var colors = new[] { Art.Hex("4F6B3A"), Art.Hex("B5473A"), Art.Hex("4A5866") };
            int n = 1 + random.Next(3);
            for (int i = 0; i < n; i++)
            {
                var o = new Vector3((i - (n - 1) * 0.5f) * 0.2f, 0f, ((float)random.NextDouble() - 0.5f) * 0.12f);
                var c = colors[random.Next(colors.Length)];
                b.Cylinder(at + o, 0.09f, 0.26f, 8, c, MeshBuilder.Shade(c, 1.15f));
                b.Cylinder(at + o + new Vector3(0f, 0.17f, 0f), 0.093f, 0.025f, 8, MeshBuilder.Shade(c, 0.7f), MeshBuilder.Shade(c, 1.15f));
            }
        }

        // Short curved wall of sandbags.
        public static void Sandbags(MeshBuilder b, Vector3 at, float yaw)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 5 - row; i++)
                {
                    float x = (i - (4 - row) * 0.5f) * 0.26f;
                    var o = new Vector3(x, 0.08f + row * 0.13f, x * x * 0.45f);
                    b.Sphere(at + r * o, new Vector3(0.15f, 0.085f, 0.11f), i % 2 == 0 ? Art.Hex("C9B07A") : Art.Hex("BBA16C"), 2, 6);
                }
        }

        // Czech hedgehog anti-tank obstacle.
        public static void TankTrap(MeshBuilder b, Vector3 at, float yaw)
        {
            var steel = Art.Hex("3C4148");
            var center = at + new Vector3(0f, 0.17f, 0f);
            const float length = 0.5f, pitch = 40f;
            for (int i = 0; i < 3; i++)
            {
                float y = yaw + i * 120f;
                var dir = Quaternion.Euler(0f, y, pitch) * Vector3.right;
                b.Barrel(center - dir * (length * 0.5f), length, 0.035f, 4, steel, y, pitch);
            }
        }
    }
}
