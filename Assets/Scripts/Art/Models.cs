using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Low-poly 3D models built once at startup. All share one vertex-colour material (Corehold/LowPoly)
    // so GPU instancing batches every copy of a mesh. Unit models face +X and have a footprint of about
    // 1 world unit; scale them by their configured size.
    public static class Models
    {
        public static Material Material { get; private set; }

        public static Mesh Soldier, SoldierLeg, Tank, TankTurret, Drone, DroneRotor;
        public static Mesh AlliedTank, AlliedTankTurret, AlliedDrone;     // player-side vehicles for the garage menu
        public const int TowerLevels = 4;
        public static readonly string[] TowerIds = { "mg", "cannon", "aa", "flame", "tesla", "mortar", "sniper", "cryo" };
        public static Mesh Pip;
        static readonly Mesh[] towerBases = new Mesh[TowerLevels];
        static readonly Mesh[,] turrets = new Mesh[TowerIds.Length, TowerLevels];

        // How a unit's parts move (see Game.Animate).
        public enum Motion { Walk, Turret, Rotors, Wheels, Heli, Tandem }

        // A unit model split into a body and up to four moving parts with their joint positions.
        public sealed class UnitRig
        {
            public Mesh Body;
            public Mesh[] Parts;
            public Vector3[] Joints;
            public Motion Motion;
        }

        static readonly Dictionary<string, UnitRig> rigs = new Dictionary<string, UnitRig>();

        public static UnitRig Rig(string enemyId) =>
            rigs.TryGetValue(enemyId, out var rig) ? rig : throw new System.InvalidOperationException("No model for enemy " + enemyId);
        public static Mesh Bullet, Shell, Missile, Flame, MortarShell;

        public static Mesh Projectile(string kind) =>
            kind == "shell" ? Shell : kind == "missile" ? Missile : kind == "flame" ? Flame : kind == "mortar" ? MortarShell : Bullet;

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

            Soldier = Make("Soldier", BuildSoldierBody);
            SoldierLeg = Make("SoldierLeg", BuildSoldierLeg);
            Tank = Make("Tank", BuildTank);
            TankTurret = Make("TankTurret", BuildTankTurret);
            Drone = Make("Drone", BuildDrone);
            DroneRotor = Make("DroneRotor", BuildDroneRotor);
            AlliedTank = Make("AlliedTank", b => BuildTankHull(b, Allied));
            AlliedTankTurret = Make("AlliedTankTurret", b => BuildTankTurret(b, Allied));
            AlliedDrone = Make("AlliedDrone", b => BuildDrone(b, Allied.Main, Allied.Light, Allied.Dark));
            var turretBuilders = new System.Action<MeshBuilder, int>[]
            {
                BuildMgTurret, BuildCannonTurret, BuildAaTurret, BuildFlameTurret,
                BuildTeslaTurret, BuildMortarTurret, BuildSniperTurret, BuildCryoTurret,
            };
            for (int level = 0; level < TowerLevels; level++)
            {
                int l = level;
                towerBases[l] = Make("TowerBase" + l, b => BuildTowerBase(b, l));
                for (int i = 0; i < TowerIds.Length; i++)
                {
                    var build = turretBuilders[i];
                    turrets[i, l] = Make(TowerIds[i] + "Turret" + l, b => build(b, l));
                }
            }
            BuildRigs();
            Pip = Make("Pip", b => b.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.09f, 0.06f, 0.09f), Glow(Art.Palette.Gold, 0.7f)));
            Bullet = Make("Bullet", b => b.Box(Vector3.zero, new Vector3(0.32f, 0.05f, 0.05f), Glow(Art.Hex("FFE27A"))));
            Shell = Make("Shell", b => b.Sphere(Vector3.zero, Vector3.one * 0.09f, Art.Hex("2E2E2E"), 3, 6));
            Missile = Make("Missile", BuildMissile);
            Flame = Make("Flame", b => b.Sphere(Vector3.zero, Vector3.one * 0.13f, Glow(Art.Hex("FF8A3D"), 0.9f), 3, 6));
            MortarShell = Make("MortarShell", b =>
            {
                b.Sphere(Vector3.zero, new Vector3(0.14f, 0.1f, 0.1f), Art.Hex("3A3A30"), 3, 6);
                b.Box(new Vector3(-0.12f, 0f, 0f), new Vector3(0.08f, 0.12f, 0.02f), Art.Hex("2A2A22"));
            });
        }

        public static Mesh TowerBase(int level) => towerBases[Mathf.Clamp(level, 0, TowerLevels - 1)];

        public static Mesh Turret(string towerId, int level) =>
            turrets[Mathf.Max(0, System.Array.IndexOf(TowerIds, towerId)), Mathf.Clamp(level, 0, TowerLevels - 1)];

        static Mesh Make(string name, System.Action<MeshBuilder> build)
        {
            var b = new MeshBuilder();
            build(b);
            return b.ToMesh(name);
        }

        // ---------------- Enemies (desert faction) ----------------

        // Animated units are split into parts: a body plus limbs, turret or rotors that Game moves every frame.
        // Part meshes have their pivot at the joint; UnitParts says where each part attaches to the body.
        public const float SoldierHip = 0.5f, SoldierLegOffset = 0.14f;
        public static readonly Vector3 TankTurretPivot = new Vector3(-0.06f, 0.45f, 0f);
        public static readonly float DroneRotorReach = 0.4f, DroneRotorHeight = 0.09f;

        static void BuildSoldierLeg(MeshBuilder b)
        {
            b.Box(new Vector3(0f, -0.25f, 0f), new Vector3(0.24f, 0.5f, 0.18f), SandDark);
            b.Box(new Vector3(0.04f, -0.47f, 0f), new Vector3(0.3f, 0.07f, 0.19f), Art.Hex("4A3B22"));     // boot
        }

        static void BuildSoldierBody(MeshBuilder b)
        {
            b.Box(new Vector3(0f, 0.78f, 0f), new Vector3(0.34f, 0.56f, 0.6f), Sand, 0f, false, SandLight);
            b.Box(new Vector3(-0.26f, 0.82f, 0f), new Vector3(0.2f, 0.42f, 0.44f), Art.Hex("6E5B36"));
            b.Box(new Vector3(0.16f, 0.82f, 0.34f), new Vector3(0.42f, 0.15f, 0.15f), Sand);
            b.Box(new Vector3(0.16f, 0.82f, -0.34f), new Vector3(0.42f, 0.15f, 0.15f), Sand);
            b.Box(new Vector3(0.36f, 0.86f, -0.2f), new Vector3(0.66f, 0.08f, 0.08f), Gun);
            b.Sphere(new Vector3(0.03f, 1.16f, 0f), Vector3.one * 0.19f, Skin, 3, 6);
            b.Sphere(new Vector3(0f, 1.24f, 0f), new Vector3(0.25f, 0.15f, 0.25f), Art.Hex("6F5D38"), 3, 7);
        }

        // Camouflage colours of a vehicle: main, light (top faces), dark (details), barrel.
        readonly struct Camo
        {
            public readonly Color Main, Light, Dark, Barrel;
            public Camo(string main, string light, string dark, string barrel)
            {
                Main = Art.Hex(main); Light = Art.Hex(light); Dark = Art.Hex(dark); Barrel = Art.Hex(barrel);
            }
        }

        static readonly Camo Desert = new Camo("A68F5E", "BFA673", "7A6640", "4A3E28");
        static readonly Camo Allied = new Camo("5F7D9C", "7FA0C2", "3B4C61", "2A2F35");

        static void BuildTank(MeshBuilder b) => BuildTankHull(b, Desert);

        static void BuildTankHull(MeshBuilder b, Camo camo)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(0f, 0.14f, 0.36f * side), new Vector3(1.02f, 0.28f, 0.22f), Track);
                for (int i = -2; i <= 2; i++)
                    b.Box(new Vector3(i * 0.2f, 0.12f, 0.475f * side), new Vector3(0.14f, 0.14f, 0.01f), Art.Hex("4A4A4A"));
            }
            b.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.92f, 0.22f, 0.6f), camo.Main, 0f, false, camo.Light);
            b.Box(new Vector3(0.42f, 0.3f, 0f), new Vector3(0.12f, 0.16f, 0.56f), camo.Dark);
            b.Box(new Vector3(-0.4f, 0.47f, 0.18f), new Vector3(0.1f, 0.06f, 0.14f), camo.Barrel);      // exhaust
        }

        // Pivot at the turret ring (TankTurretPivot on the hull).
        static void BuildTankTurret(MeshBuilder b) => BuildTankTurret(b, Desert);

        static void BuildTankTurret(MeshBuilder b, Camo camo)
        {
            b.Cylinder(Vector3.zero, 0.26f, 0.17f, 8, MeshBuilder.Shade(camo.Main, 0.9f), camo.Main);
            b.Barrel(new Vector3(0.2f, 0.09f, 0f), 0.62f, 0.045f, 6, camo.Barrel);
            b.Barrel(new Vector3(0.76f, 0.09f, 0f), 0.08f, 0.065f, 6, MeshBuilder.Shade(camo.Barrel, 0.8f));
            b.Cylinder(new Vector3(-0.08f, 0.17f, 0.1f), 0.08f, 0.04f, 6, camo.Dark);
        }

        static void BuildDroneRotor(MeshBuilder b)
        {
            b.Box(Vector3.zero, new Vector3(0.46f, 0.012f, 0.06f), Art.Hex("C9D3DC"));
            b.Box(Vector3.zero, new Vector3(0.06f, 0.012f, 0.46f), Art.Hex("C9D3DC"));
            b.Cylinder(new Vector3(0f, -0.01f, 0f), 0.03f, 0.03f, 6, Gun);
        }

        static void BuildDrone(MeshBuilder b) => BuildDrone(b, Red, Art.Hex("E25A4F"), Art.Hex("8E2F24"));

        static void BuildDrone(MeshBuilder b, Color body, Color top, Color dark)
        {
            b.Box(Vector3.zero, new Vector3(1.15f, 0.05f, 0.07f), Gun, 45f);
            b.Box(Vector3.zero, new Vector3(1.15f, 0.05f, 0.07f), Gun, -45f);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var c = new Vector3(DroneRotorReach * sx, 0.03f, DroneRotorReach * sz);
                b.Cylinder(c, 0.05f, 0.06f, 6, Gun);
                // Faint disc of the spinning blades.
                b.Cylinder(c + new Vector3(0f, 0.055f, 0f), 0.23f, 0.004f, 10, Glow(Art.Hex("DCE4EA"), 0.15f));
            }
            b.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.16f, 0.3f), body, 0f, false, top);
            b.Box(new Vector3(-0.05f, 0.12f, 0f), new Vector3(0.2f, 0.05f, 0.18f), dark);
            b.Sphere(new Vector3(0.22f, -0.02f, 0f), Vector3.one * 0.06f, Glow(Art.Hex("7FDBFF"), 0.8f), 3, 6);
        }

        // ---------------- Player towers (steel blue) ----------------

        // Towers grow with every upgrade: armour skirt (1), corner lights and plating (2), gold trim and antenna (3);
        // turrets gain barrels, tubes, armour and a radar.
        static readonly Color Gold = Art.Hex("E8B84A");

        static void BuildTowerBase(MeshBuilder b, int level)
        {
            b.Cylinder(Vector3.zero, 0.47f, 0.22f, 8, SteelDark, Art.Hex("47586E"));
            b.Cylinder(new Vector3(0f, 0.22f, 0f), 0.4f, 0.1f, 8, Art.Hex("4F6178"), Art.Hex("5C6F88"));
            if (level >= 1)
                b.Cylinder(Vector3.zero, 0.52f, 0.12f, 8, Art.Hex("34404F"), Art.Hex("56677D"));
            if (level >= 2)
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + i * 90f) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.42f;
                    b.Box(p + new Vector3(0f, 0.17f, 0f), new Vector3(0.12f, 0.34f, 0.12f), SteelLight, -a * Mathf.Rad2Deg);
                    b.Box(p + new Vector3(0f, 0.35f, 0f), new Vector3(0.07f, 0.04f, 0.07f), Glow(Art.Hex("6FD3FF"), 0.8f), -a * Mathf.Rad2Deg);
                }
            if (level >= 3)
            {
                b.Cylinder(new Vector3(0f, 0.3f, 0f), 0.42f, 0.03f, 8, Gold, Art.Hex("F5D27A"));
                b.Cylinder(new Vector3(-0.3f, 0.22f, -0.2f), 0.015f, 0.55f, 4, Gun);
                b.Sphere(new Vector3(-0.3f, 0.79f, -0.2f), Vector3.one * 0.04f, Glow(Red), 2, 5);
            }
        }

        static void BuildMgTurret(MeshBuilder b, int level)
        {
            float r = 0.24f + 0.02f * level;
            b.Cylinder(Vector3.zero, r, 0.2f, 8, Steel, level >= 3 ? Gold : SteelLight);
            b.Box(new Vector3(0.16f, 0.12f, 0f), new Vector3(0.14f, 0.14f, 0.24f + 0.04f * level), SteelDark);
            float length = 0.48f + 0.06f * level;
            int rows = level >= 2 ? 2 : 1;
            for (int row = 0; row < rows; row++)
                for (int side = -1; side <= 1; side += 2)
                    b.Barrel(new Vector3(0.2f, 0.11f + row * 0.08f + (rows == 1 ? 0.02f : 0f), 0.065f * side), length, 0.03f, 6, Gun);
            b.Box(new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.12f, 0.08f, 0.16f), Art.Hex("2D3A4A"));
            if (level >= 1)
            {
                // Gun shield and an ammo box.
                b.Box(new Vector3(0.26f, 0.16f, 0f), new Vector3(0.04f, 0.2f, 0.36f), SteelLight);
                b.Box(new Vector3(-0.05f, 0.12f, -0.26f), new Vector3(0.18f, 0.12f, 0.1f), Art.Hex("4F6B3A"));
            }
        }

        static void BuildCannonTurret(MeshBuilder b, int level)
        {
            float w = 0.46f + 0.04f * level;
            b.Box(new Vector3(-0.04f, 0.12f, 0f), new Vector3(w, 0.24f, 0.4f + 0.04f * level), Steel, 0f, false, level >= 3 ? Gold : SteelLight);
            b.Box(new Vector3(-0.1f, 0.27f, 0f), new Vector3(0.24f, 0.07f, 0.26f), SteelDark);
            float length = 0.55f + 0.08f * level, radius = 0.07f + 0.008f * level;
            var barrels = level >= 3 ? new[] { -0.09f, 0.09f } : new[] { 0f };
            foreach (var z in barrels)
            {
                b.Barrel(new Vector3(0.18f, 0.14f, z), length, radius, 8, Gun);
                b.Barrel(new Vector3(0.18f + length - 0.05f, 0.14f, z), 0.1f + (level >= 1 ? 0.04f : 0f), radius + 0.025f, 8, Art.Hex("161A1F"));
            }
            if (level >= 2)
                for (int side = -1; side <= 1; side += 2)
                    b.Box(new Vector3(-0.02f, 0.13f, side * (0.24f + 0.02f * level)), new Vector3(w * 0.9f, 0.2f, 0.05f), SteelLight);
        }

        static void BuildAaTurret(MeshBuilder b, int level)
        {
            b.Cylinder(Vector3.zero, 0.22f, 0.12f, 8, SteelDark);
            int rows = level >= 3 ? 4 : level >= 1 ? 3 : 2;
            float h = 0.12f * rows;
            b.Box(new Vector3(0f, 0.15f + h * 0.5f, 0f), new Vector3(0.4f, h + 0.06f, 0.44f), Steel, 0f, false, level >= 3 ? Gold : SteelLight);
            for (int row = 0; row < rows; row++)
            for (int col = -1; col <= 1; col += 2)
            {
                var start = new Vector3(0.2f, 0.21f + row * 0.12f, 0.1f * col);
                b.Barrel(start, 0.12f, 0.045f, 6, White);
                b.Barrel(start + new Vector3(0.12f, 0f, 0f), 0.05f, 0.047f, 6, Glow(Red, 0.4f));
            }
            if (level >= 2)
            {
                // Search radar on a mast.
                float top = 0.18f + h;
                b.Cylinder(new Vector3(-0.12f, top, 0f), 0.025f, 0.18f, 5, Gun);
                b.Box(new Vector3(-0.12f, top + 0.2f, 0f), new Vector3(0.05f, 0.12f, 0.3f), Art.Hex("D9DEE2"));
            }
        }

        static void BuildFlameTurret(MeshBuilder b, int level)
        {
            b.Cylinder(Vector3.zero, 0.22f, 0.14f, 8, Steel, level >= 3 ? Gold : SteelLight);
            int tanks = level >= 2 ? 3 : 2;
            for (int i = 0; i < tanks; i++)
            {
                float z = (i - (tanks - 1) * 0.5f) * 0.13f;
                b.Cylinder(new Vector3(-0.13f, 0.1f, z), 0.065f, 0.3f, 8, Art.Hex("B5473A"), Art.Hex("D0614F"));
            }
            float length = 0.36f + 0.05f * level;
            b.Box(new Vector3(0.05f, 0.18f, 0f), new Vector3(0.2f, 0.12f, 0.16f), SteelDark);
            b.Barrel(new Vector3(0.12f, 0.18f, 0f), length, 0.05f, 6, Gun);
            b.Barrel(new Vector3(0.1f + length, 0.18f, 0f), 0.06f, 0.065f, 6, Glow(Art.Palette.Explosion, 0.5f));
            if (level >= 1) b.Box(new Vector3(0.22f, 0.2f, 0f), new Vector3(0.04f, 0.18f, 0.3f), SteelLight);
        }

        static void BuildTeslaTurret(MeshBuilder b, int level)
        {
            var copper = Art.Hex("C87533");
            b.Cylinder(Vector3.zero, 0.22f, 0.1f, 8, SteelDark, level >= 3 ? Gold : Steel);
            float height = 0.38f + 0.06f * level;
            b.Cylinder(new Vector3(0f, 0.1f, 0f), 0.06f, height, 6, Steel);
            int rings = 3 + level;
            for (int i = 0; i < rings; i++)
                b.Cylinder(new Vector3(0f, 0.14f + i * (height - 0.1f) / rings, 0f), 0.15f - i * 0.012f, 0.035f, 10, copper, Art.Hex("E09050"));
            b.Sphere(new Vector3(0f, 0.16f + height, 0f), Vector3.one * (0.1f + 0.015f * level), Glow(Art.Hex("8FE3FF"), 0.9f), 3, 8);
            if (level >= 2)
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f * Mathf.Deg2Rad;
                    b.Box(new Vector3(Mathf.Cos(a) * 0.18f, 0.2f, Mathf.Sin(a) * 0.18f), new Vector3(0.04f, 0.3f, 0.04f), copper);
                    b.Sphere(new Vector3(Mathf.Cos(a) * 0.18f, 0.37f, Mathf.Sin(a) * 0.18f), Vector3.one * 0.035f, Glow(Art.Hex("8FE3FF"), 0.8f), 2, 5);
                }
        }

        static void BuildMortarTurret(MeshBuilder b, int level)
        {
            b.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.46f, 0.08f, 0.46f), SteelDark, 0f, false, level >= 3 ? Gold : Steel);
            float length = 0.42f + 0.05f * level, radius = 0.08f + 0.012f * level;
            var tubes = level >= 2 ? new[] { -0.11f, 0.11f } : new[] { 0f };
            foreach (var z in tubes)
            {
                b.Barrel(new Vector3(-0.08f, 0.1f, z), length, radius, 8, Gun, 0f, 58f);
                b.Box(new Vector3(0.08f, 0.12f, z), new Vector3(0.04f, 0.16f, 0.04f), SteelDark);
            }
            if (level >= 1)
                for (int i = 0; i < 2; i++)
                    b.Box(new Vector3(-0.16f, 0.13f, -0.16f + i * 0.32f), new Vector3(0.12f, 0.1f, 0.08f), Art.Hex("4F6B3A"));
        }

        static void BuildSniperTurret(MeshBuilder b, int level)
        {
            b.Cylinder(Vector3.zero, 0.18f, 0.1f, 8, SteelDark);
            b.Box(new Vector3(0f, 0.16f, 0f), new Vector3(0.34f, 0.12f, 0.16f), Steel, 0f, false, level >= 3 ? Gold : SteelLight);
            float length = 0.75f + 0.08f * level;
            b.Barrel(new Vector3(0.14f, 0.17f, 0f), length, 0.025f, 6, Gun);
            b.Barrel(new Vector3(-0.08f, 0.26f, 0f), 0.24f, 0.035f, 6, Art.Hex("1E2228"));
            b.Box(new Vector3(0.165f, 0.26f, 0f), new Vector3(0.01f, 0.05f, 0.05f), Glow(Art.Hex("7FDBFF"), 0.7f));
            if (level >= 1) b.Barrel(new Vector3(0.1f + length, 0.17f, 0f), 0.08f, 0.04f, 6, Art.Hex("161A1F"));
            if (level >= 2)
                for (int side = -1; side <= 1; side += 2)
                    b.Box(new Vector3(0.4f, 0.09f, 0.06f * side), new Vector3(0.03f, 0.16f, 0.03f), SteelDark);
        }

        static void BuildCryoTurret(MeshBuilder b, int level)
        {
            var ice = Art.Hex("BFE6F7");
            b.Cylinder(Vector3.zero, 0.22f, 0.1f, 8, SteelDark, level >= 3 ? Gold : Steel);
            b.Cylinder(new Vector3(0f, 0.1f, 0f), 0.17f, 0.22f, 8, Art.Hex("8FB8CC"), ice, 0.09f);
            int crystals = 3 + level;
            for (int i = 0; i < crystals; i++)
            {
                float a = i * Mathf.PI * 2f / crystals;
                b.Cylinder(new Vector3(Mathf.Cos(a) * 0.17f, 0.1f, Mathf.Sin(a) * 0.17f), 0.035f, 0.2f + 0.03f * (i % 2), 4, Glow(ice, 0.45f), null, 0f);
            }
            b.Sphere(new Vector3(0f, 0.38f, 0f), Vector3.one * (0.08f + 0.012f * level), Glow(Art.Hex("9EE8FF"), 0.9f), 3, 6);
        }

        // ---------------- Enemy rigs ----------------

        static readonly Camo Heavy = new Camo("5E5048", "7A6A5E", "3E342E", "2A2420");

        static void BuildRigs()
        {
            float r = DroneRotorReach;
            rigs["soldier"] = new UnitRig
            {
                Body = Soldier, Motion = Motion.Walk,
                Parts = new[] { SoldierLeg, SoldierLeg },
                Joints = new[] { new Vector3(0f, SoldierHip, SoldierLegOffset), new Vector3(0f, SoldierHip, -SoldierLegOffset) },
            };
            rigs["tank"] = new UnitRig { Body = Tank, Motion = Motion.Turret, Parts = new[] { TankTurret }, Joints = new[] { TankTurretPivot } };
            rigs["drone"] = new UnitRig
            {
                Body = Drone, Motion = Motion.Rotors,
                Parts = new[] { DroneRotor, DroneRotor, DroneRotor, DroneRotor },
                Joints = new[]
                {
                    new Vector3(r, DroneRotorHeight, r), new Vector3(r, DroneRotorHeight, -r),
                    new Vector3(-r, DroneRotorHeight, r), new Vector3(-r, DroneRotorHeight, -r),
                },
            };
            var wheel = Make("Wheel", b =>
            {
                b.Barrel(new Vector3(0f, 0f, 0.05f), 0.1f, 0.14f, 8, Art.Hex("24262A"), 90f);
                b.Barrel(new Vector3(0f, 0f, 0.055f), 0.11f, 0.06f, 6, Art.Hex("8A8F96"), 90f);
            });
            rigs["jeep"] = new UnitRig
            {
                Body = Make("Jeep", BuildJeep), Motion = Motion.Wheels,
                Parts = new[] { wheel, wheel, wheel, wheel },
                Joints = new[] { new Vector3(0.3f, 0.14f, 0.27f), new Vector3(0.3f, 0.14f, -0.27f), new Vector3(-0.3f, 0.14f, 0.27f), new Vector3(-0.3f, 0.14f, -0.27f) },
            };
            rigs["apc"] = new UnitRig
            {
                Body = Make("Apc", BuildApc), Motion = Motion.Wheels,
                Parts = new[] { wheel, wheel, wheel, wheel },
                Joints = new[] { new Vector3(0.32f, 0.15f, 0.31f), new Vector3(0.32f, 0.15f, -0.31f), new Vector3(-0.32f, 0.15f, 0.31f), new Vector3(-0.32f, 0.15f, -0.31f) },
            };
            rigs["heli"] = new UnitRig
            {
                Body = Make("Heli", BuildHeli), Motion = Motion.Heli,
                Parts = new[]
                {
                    Make("HeliRotor", b =>
                    {
                        b.Box(Vector3.zero, new Vector3(1.5f, 0.015f, 0.07f), Gun);
                        b.Box(Vector3.zero, new Vector3(0.07f, 0.015f, 1.5f), Gun);
                        b.Cylinder(new Vector3(0f, -0.05f, 0f), 0.05f, 0.06f, 6, Gun);
                    }),
                    Make("HeliTailRotor", b =>
                    {
                        b.Box(Vector3.zero, new Vector3(0.32f, 0.04f, 0.01f), Gun);
                        b.Box(Vector3.zero, new Vector3(0.04f, 0.32f, 0.01f), Gun);
                    }),
                },
                Joints = new[] { new Vector3(0f, 0.42f, 0f), new Vector3(-0.86f, 0.3f, 0.06f) },
            };
            rigs["boss"] = new UnitRig
            {
                Body = Make("Boss", b => { BuildTankHull(b, Heavy); BossArmor(b); }), Motion = Motion.Turret,
                Parts = new[] { Make("BossTurret", BuildBossTurret) },
                Joints = new[] { TankTurretPivot + new Vector3(0f, 0.02f, 0f) },
            };
            BuildBossRigs();
        }

        // Bosses: a tandem-rotor gunship and a three-turret super tank.
        static void BuildBossRigs()
        {
            var rotor = Make("GunshipRotor", b =>
            {
                for (int i = 0; i < 3; i++)
                    b.Box(Vector3.zero, new Vector3(1.3f, 0.015f, 0.08f), Gun, i * 60f);
                b.Cylinder(new Vector3(0f, -0.06f, 0f), 0.06f, 0.08f, 6, Gun);
            });
            rigs["gunship"] = new UnitRig
            {
                Body = Make("Gunship", BuildGunship), Motion = Motion.Tandem,
                Parts = new[] { rotor, rotor },
                Joints = new[] { new Vector3(0.48f, 0.5f, 0f), new Vector3(-0.5f, 0.62f, 0f) },
            };
            var mini = Make("TitanMiniTurret", b =>
            {
                b.Cylinder(Vector3.zero, 0.13f, 0.1f, 8, Titan.Dark, Titan.Main);
                b.Barrel(new Vector3(0.08f, 0.06f, 0f), 0.34f, 0.03f, 5, Titan.Barrel);
            });
            rigs["titan"] = new UnitRig
            {
                Body = Make("Titan", BuildTitanHull), Motion = Motion.Turret,
                Parts = new[] { Make("TitanTurret", BuildTitanTurret), mini, mini },
                Joints = new[] { new Vector3(-0.05f, 0.58f, 0f), new Vector3(0.38f, 0.5f, 0.24f), new Vector3(0.38f, 0.5f, -0.24f) },
            };
        }

        static readonly Camo Titan = new Camo("3F4744", "56605B", "2A302D", "1C201E");

        static void BuildGunship(MeshBuilder b)
        {
            var body = Art.Hex("4E5A3A");
            b.Box(new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.34f, 0.38f), body, 0f, false, Art.Hex("606E48"));
            b.Sphere(new Vector3(0.62f, 0.24f, 0f), new Vector3(0.2f, 0.17f, 0.19f), body, 3, 8);
            b.Sphere(new Vector3(0.7f, 0.3f, 0f), new Vector3(0.12f, 0.08f, 0.15f), Glow(Art.Hex("9CC9E0"), 0.3f), 3, 6);
            b.Box(new Vector3(0.48f, 0.44f, 0f), new Vector3(0.24f, 0.12f, 0.2f), Art.Hex("3E4A2C"));        // front pylon
            b.Box(new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.3f, 0.28f, 0.22f), Art.Hex("3E4A2C"));         // rear pylon
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(0.1f, 0.12f, 0.26f * side), new Vector3(0.5f, 0.12f, 0.12f), Art.Hex("3A4428"));    // sponsons
                b.Barrel(new Vector3(0.3f, 0.1f, 0.3f * side), 0.3f, 0.03f, 5, Gun);
                b.Box(new Vector3(-0.1f, 0.02f, 0.2f * side), new Vector3(0.9f, 0.03f, 0.03f), Gun);
            }
            for (int i = 0; i < 4; i++)
                b.Box(new Vector3(-0.3f + i * 0.2f, 0.3f, 0.195f), new Vector3(0.08f, 0.06f, 0.01f), Glow(Art.Hex("FFD27A"), 0.6f));
        }

        static void BuildTitanHull(MeshBuilder b)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(0f, 0.16f, 0.4f * side), new Vector3(1.15f, 0.32f, 0.26f), Track);
                b.Box(new Vector3(0f, 0.3f, 0.54f * side), new Vector3(1.15f, 0.22f, 0.04f), Titan.Dark);
                for (int i = -2; i <= 2; i++)
                    b.Box(new Vector3(i * 0.22f, 0.3f, 0.565f * side), new Vector3(0.08f, 0.08f, 0.01f), Glow(Art.Hex("FF4A3A"), i == 0 ? 0.8f : 0f));
            }
            b.Box(new Vector3(0f, 0.4f, 0f), new Vector3(1.05f, 0.28f, 0.7f), Titan.Main, 0f, false, Titan.Light);
            b.Box(new Vector3(0.52f, 0.34f, 0f), new Vector3(0.08f, 0.2f, 0.72f), Titan.Dark);
            for (int side = -1; side <= 1; side += 2)
                b.Cylinder(new Vector3(-0.48f, 0.5f, 0.22f * side), 0.05f, 0.18f, 6, Titan.Barrel);       // exhausts
        }

        static void BuildTitanTurret(MeshBuilder b)
        {
            b.Cylinder(Vector3.zero, 0.36f, 0.22f, 8, Titan.Main, Titan.Light);
            b.Box(new Vector3(-0.1f, 0.18f, 0f), new Vector3(0.46f, 0.14f, 0.5f), Titan.Dark, 0f, false, Titan.Main);
            b.Barrel(new Vector3(0.24f, 0.12f, 0f), 0.9f, 0.065f, 8, Titan.Barrel);
            b.Barrel(new Vector3(1.04f, 0.12f, 0f), 0.14f, 0.09f, 8, Art.Hex("101210"));
            b.Box(new Vector3(-0.2f, 0.28f, 0.16f), new Vector3(0.14f, 0.1f, 0.14f), Titan.Dark);
            b.Sphere(new Vector3(-0.2f, 0.36f, 0.16f), Vector3.one * 0.04f, Glow(Art.Hex("FF4A3A")), 2, 5);
        }

        static void BuildJeep(MeshBuilder b)
        {
            b.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.86f, 0.18f, 0.5f), Sand, 0f, false, SandLight);
            b.Box(new Vector3(0.32f, 0.36f, 0f), new Vector3(0.24f, 0.04f, 0.46f), SandDark);                // hood
            b.Box(new Vector3(0.14f, 0.45f, 0f), new Vector3(0.03f, 0.18f, 0.46f), Art.Hex("3A3020"));      // windscreen frame
            b.Box(new Vector3(0.15f, 0.45f, 0f), new Vector3(0.01f, 0.14f, 0.4f), Glow(Art.Hex("9CC9E0"), 0.25f));
            b.Box(new Vector3(-0.22f, 0.5f, 0f), new Vector3(0.04f, 0.26f, 0.46f), Art.Hex("3A3020"));      // roll bar
            b.Barrel(new Vector3(-0.2f, 0.66f, 0f), 0.36f, 0.025f, 5, Gun);                                 // mounted gun
            b.Sphere(new Vector3(-0.05f, 0.5f, 0.12f), Vector3.one * 0.09f, Skin, 2, 5);
            b.Sphere(new Vector3(-0.05f, 0.56f, 0.12f), new Vector3(0.11f, 0.06f, 0.11f), Art.Hex("6F5D38"), 2, 6);
        }

        static void BuildApc(MeshBuilder b)
        {
            b.Box(new Vector3(-0.05f, 0.38f, 0f), new Vector3(0.9f, 0.32f, 0.56f), Sand, 0f, false, SandLight);
            b.Box(new Vector3(0.44f, 0.32f, 0f), new Vector3(0.16f, 0.2f, 0.54f), SandDark);                  // sloped nose
            b.Box(new Vector3(-0.5f, 0.38f, 0f), new Vector3(0.04f, 0.24f, 0.3f), Art.Hex("6E5B36"));         // rear door
            b.Cylinder(new Vector3(0.05f, 0.54f, 0f), 0.13f, 0.1f, 8, SandDark, SandLight);
            b.Barrel(new Vector3(0.12f, 0.6f, 0f), 0.4f, 0.025f, 5, Gun);
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(0.1f, 0.44f, 0.285f * side), new Vector3(0.12f, 0.06f, 0.01f), Glow(Art.Hex("9CC9E0"), 0.2f));
        }

        static void BuildHeli(MeshBuilder b)
        {
            var body = Art.Hex("6B7A4A");
            b.Sphere(new Vector3(0.2f, 0.2f, 0f), new Vector3(0.36f, 0.22f, 0.22f), body, 4, 8);
            b.Box(new Vector3(-0.1f, 0.2f, 0f), new Vector3(0.5f, 0.24f, 0.3f), body, 0f, false, Art.Hex("7E8E58"));
            b.Sphere(new Vector3(0.38f, 0.26f, 0f), new Vector3(0.16f, 0.12f, 0.17f), Glow(Art.Hex("9CC9E0"), 0.3f), 3, 6);
            b.Barrel(new Vector3(-0.3f, 0.26f, 0f), 0.56f, 0.05f, 6, body, 180f);
            b.Box(new Vector3(-0.84f, 0.36f, 0f), new Vector3(0.16f, 0.22f, 0.03f), Art.Hex("54613A"));
            b.Cylinder(new Vector3(0f, 0.32f, 0f), 0.06f, 0.1f, 6, Gun);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(0f, 0.02f, 0.16f * side), new Vector3(0.6f, 0.03f, 0.03f), Gun);
                b.Box(new Vector3(0.05f, 0.18f, 0.24f * side), new Vector3(0.16f, 0.07f, 0.12f), Art.Hex("3E4A2C"));   // rocket pods
            }
        }

        static void BossArmor(MeshBuilder b)
        {
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(0f, 0.28f, 0.49f * side), new Vector3(1.0f, 0.18f, 0.04f), Heavy.Dark);       // side skirts
            b.Box(new Vector3(0.48f, 0.36f, 0f), new Vector3(0.06f, 0.14f, 0.62f), Art.Hex("2A2420"));
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(0.5f, 0.38f, 0.22f * side), new Vector3(0.03f, 0.05f, 0.08f), Glow(Art.Hex("FF4A3A"), 0.8f));
        }

        static void BuildBossTurret(MeshBuilder b)
        {
            b.Cylinder(Vector3.zero, 0.33f, 0.2f, 8, MeshBuilder.Shade(Heavy.Main, 0.9f), Heavy.Light);
            b.Box(new Vector3(-0.05f, 0.16f, 0f), new Vector3(0.4f, 0.12f, 0.42f), Heavy.Main, 0f, false, Heavy.Light);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Barrel(new Vector3(0.22f, 0.12f, 0.08f * side), 0.72f, 0.045f, 6, Heavy.Barrel);
                b.Barrel(new Vector3(0.88f, 0.12f, 0.08f * side), 0.1f, 0.065f, 6, Art.Hex("181410"));
            }
            b.Cylinder(new Vector3(-0.2f, 0.22f, 0.18f), 0.015f, 0.5f, 4, Gun);
            b.Sphere(new Vector3(-0.2f, 0.73f, 0.18f), Vector3.one * 0.035f, Glow(Red), 2, 5);
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
