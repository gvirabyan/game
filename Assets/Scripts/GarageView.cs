using UnityEngine;

namespace Corehold
{
    // Main menu backdrop: a hangar with the player's vehicles. A tank turns on a turntable, towers scan from
    // their pedestals, a drone hovers, the beacon spins and the camera drifts slowly.
    public sealed class GarageView : MonoBehaviour
    {
        static readonly Color Background = Art.Hex("1A2029");
        static readonly Color Floor = Art.Hex("5C6168"), FloorSeam = Art.Hex("4A4F56"), Wall = Art.Hex("3D4552"), Beam = Art.Hex("2A313B");
        static readonly Color Yellow = Art.Hex("F2C14E"), Black = Art.Hex("23262B");
        static readonly Vector3 Focus = new Vector3(2.6f, 0.9f, 0.6f);

        Camera cam;
        Vector3 camHome;
        Transform turntable, drone, beacon;
        Transform[] turrets, rotors;

        public static GarageView Build(Transform parent, Camera cam)
        {
            var root = new GameObject("Garage");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GarageView>();
            view.cam = cam;
            view.Create();
            return view;
        }

        void Create()
        {
            var b = new MeshBuilder();
            Hangar(b);
            var go = new GameObject("Hangar");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Hangar");
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;

            // Turntable with the tank.
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(transform, false);
            turntable.localPosition = new Vector3(3f, 0f, 1f);
            var deck = new MeshBuilder();
            deck.Cylinder(Vector3.zero, 2.1f, 0.14f, 24, Art.Hex("3A4048"), Art.Hex("474E57"));
            deck.Cylinder(new Vector3(0f, 0.14f, 0f), 1.8f, 0.01f, 24, Art.Hex("525A64"));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                deck.Box(new Vector3(Mathf.Cos(a) * 1.95f, 0.15f, Mathf.Sin(a) * 1.95f), new Vector3(0.12f, 0.02f, 0.12f), Glow(Art.Hex("6FD3FF")), -a * Mathf.Rad2Deg);
            }
            Part("Deck", deck.ToMesh("Turntable"), turntable, Vector3.zero, 1f);
            var tank = Part("Tank", Models.AlliedTank, turntable, new Vector3(0f, 0.15f, 0f), 2.2f);
            var tankTurret = Part("TankTurret", Models.AlliedTankTurret, tank, Models.TankTurretPivot, 1f);

            // Towers on pedestals.
            var spots = new[] { new Vector3(-0.6f, 0f, 2.6f), new Vector3(6.6f, 0f, 2.4f), new Vector3(6.2f, 0f, -1.2f) };
            var turretMeshes = new[] { Models.Turret("mg", 3), Models.Turret("cannon", 3), Models.Turret("aa", 3) };
            turrets = new Transform[spots.Length + 1];
            for (int i = 0; i < spots.Length; i++)
            {
                var ped = new MeshBuilder();
                ped.Cylinder(Vector3.zero, 0.75f, 0.3f, 8, Art.Hex("444B55"), Art.Hex("525A65"));
                ped.Cylinder(new Vector3(0f, 0.3f, 0f), 0.68f, 0.015f, 8, Glow(Yellow, 0.3f));
                var pedestal = Part("Pedestal", ped.ToMesh("Pedestal"), transform, spots[i], 1f);
                var tower = Part("Tower", Models.TowerBase(3), pedestal, new Vector3(0f, 0.31f, 0f), 1.4f);
                turrets[i] = Part("Turret", turretMeshes[i], tower, new Vector3(0f, 0.32f, 0f), 1f);
            }
            turrets[spots.Length] = tankTurret;

            // Hovering drone.
            drone = Part("Drone", Models.AlliedDrone, transform, new Vector3(0.8f, 2.4f, -0.6f), 1.5f);
            rotors = new Transform[4];
            float r = Models.DroneRotorReach;
            for (int i = 0; i < 4; i++)
                rotors[i] = Part("Rotor", Models.DroneRotor, drone, new Vector3(i < 2 ? r : -r, Models.DroneRotorHeight, i % 2 == 0 ? r : -r), 1f);

            // Rotating warning beacon over the door.
            var bb = new MeshBuilder();
            bb.Cylinder(Vector3.zero, 0.12f, 0.12f, 8, Glow(Art.Hex("FF9F2E"), 0.6f));
            bb.Box(new Vector3(0.1f, 0.06f, 0f), new Vector3(0.14f, 0.1f, 0.05f), Glow(Art.Hex("FFE2A8")));
            beacon = Part("Beacon", bb.ToMesh("Beacon"), transform, new Vector3(-4.6f, 4.15f, 4.7f), 1f);

            // Warm work lights.
            Lamp(new Vector3(3f, 4f, 0.5f), Art.Hex("FFD9A0"), 9f, 1.3f);
            Lamp(new Vector3(-2.5f, 4f, 1.5f), Art.Hex("BFD8FF"), 8f, 0.9f);

            PlaceCamera();
        }

        void PlaceCamera()
        {
            cam.orthographic = false;
            cam.fieldOfView = 38f;
            cam.backgroundColor = Background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 60f;
            // Wider screens see more of the hangar; narrower ones step back so the turntable stays in view.
            float back = Mathf.Lerp(12.5f, 9.5f, Mathf.InverseLerp(1.33f, 2.2f, cam.aspect));
            camHome = Focus + new Vector3(-2.2f, 2.6f, -back);
            cam.transform.position = camHome;
            cam.transform.LookAt(Focus + new Vector3(-1.4f, 0f, 0f));
            RenderSettings.fog = false;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            turntable.localRotation = Quaternion.Euler(0f, t * 14f, 0f);
            for (int i = 0; i < turrets.Length; i++)
                turrets[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.5f + i * 1.7f) * 50f + i * 40f, 0f);
            drone.localPosition = new Vector3(0.8f + Mathf.Sin(t * 0.4f) * 0.5f, 2.4f + Mathf.Sin(t * 1.6f) * 0.12f, -0.6f);
            drone.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 4f, t * 20f, Mathf.Sin(t * 0.9f) * 4f);
            for (int i = 0; i < rotors.Length; i++)
                rotors[i].localRotation = Quaternion.Euler(0f, t * (i % 2 == 0 ? 1500f : -1500f), 0f);
            beacon.localRotation = Quaternion.Euler(0f, t * 240f, 0f);

            cam.transform.position = camHome + new Vector3(Mathf.Sin(t * 0.21f) * 0.6f, Mathf.Sin(t * 0.17f) * 0.15f, Mathf.Sin(t * 0.13f) * 0.3f);
            cam.transform.LookAt(Focus + new Vector3(-1.4f, 0f, 0f));
        }

        // ---------------- Static hangar ----------------

        static void Hangar(MeshBuilder b)
        {
            // Concrete floor with seams and yellow parking lines.
            b.Box(new Vector3(0f, -0.05f, 0f), new Vector3(24f, 0.1f, 14f), Floor);
            for (float x = -12f; x <= 12f; x += 2f) b.Box(new Vector3(x, 0.002f, 0f), new Vector3(0.03f, 0.004f, 14f), FloorSeam);
            for (float z = -7f; z <= 7f; z += 2f) b.Box(new Vector3(0f, 0.002f, z), new Vector3(24f, 0.004f, 0.03f), FloorSeam);
            foreach (float x in new[] { -3.6f, 0.4f, 8.6f })
                b.Box(new Vector3(x, 0.005f, 0.5f), new Vector3(0.12f, 0.006f, 6f), Yellow);
            b.Box(new Vector3(2.5f, 0.005f, -2.6f), new Vector3(12.3f, 0.006f, 0.12f), Yellow);

            // Back wall with steel ribs, a hazard band and a half-open roller door showing daylight.
            b.Box(new Vector3(0f, 3f, 5f), new Vector3(24f, 6f, 0.3f), Wall);
            for (float x = -10f; x <= 12f; x += 3.2f)
                b.Box(new Vector3(x, 3f, 4.8f), new Vector3(0.3f, 6f, 0.25f), Beam);
            for (int i = 0; i < 48; i++)
                b.Box(new Vector3(-12f + i * 0.5f + 0.25f, 0.25f, 4.83f), new Vector3(0.25f, 0.5f, 0.02f), i % 2 == 0 ? Yellow : Black);
            b.Box(new Vector3(-4.6f, 1.8f, 4.86f), new Vector3(3.6f, 3.6f, 0.05f), Glow(Art.Hex("D7ECFF"), 0.85f));      // daylight
            for (int i = 0; i < 7; i++)
                b.Box(new Vector3(-4.6f, 3.55f - i * 0.18f, 4.8f), new Vector3(3.7f, 0.15f, 0.08f), Art.Hex("6E7783"));
            b.Box(new Vector3(-4.6f, 3.8f, 4.75f), new Vector3(4f, 0.3f, 0.2f), Beam);

            // Side wall and roof trusses with lamps.
            b.Box(new Vector3(-9f, 3f, 0f), new Vector3(0.3f, 6f, 14f), MeshBuilder.Shade(Wall, 0.85f));
            for (float x = -8f; x <= 10f; x += 4.5f)
            {
                b.Box(new Vector3(x, 5.2f, 0f), new Vector3(0.2f, 0.3f, 14f), Beam);
                b.Box(new Vector3(x, 4.6f, 0.5f), new Vector3(0.9f, 0.08f, 0.35f), Glow(Art.Hex("FFF1D0"), 0.9f));
                b.Box(new Vector3(x, 4.9f, 0.5f), new Vector3(0.05f, 0.6f, 0.05f), Beam);
            }

            // Workshop clutter.
            Models.Crates(b, new Vector3(-7.4f, 0f, 3.6f), 3);
            Models.Crates(b, new Vector3(-6.4f, 0f, 3.9f), 8);
            Models.Barrels(b, new Vector3(9.4f, 0f, 3.8f), 5);
            Models.Barrels(b, new Vector3(-7.8f, 0f, 1.6f), 11);
            Models.Sandbags(b, new Vector3(-1.6f, 0f, -2.2f), 20f);
            Workbench(b, new Vector3(-2.2f, 0f, 4.2f));
            Tires(b, new Vector3(9.6f, 0f, 1.6f));
            Shelf(b, new Vector3(4.2f, 0f, 4.5f));
        }

        static void Workbench(MeshBuilder b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.08f, 0.7f), Art.Hex("8A6A44"), 0f, false, Art.Hex("9C7A50"));
            foreach (var x in new[] { -0.9f, 0.9f })
                b.Box(at + new Vector3(x, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.6f), Beam);
            b.Box(at + new Vector3(-0.5f, 0.92f, 0f), new Vector3(0.4f, 0.16f, 0.26f), Art.Hex("C0392B"), 0f, false, Art.Hex("D9503F"));    // toolbox
            b.Box(at + new Vector3(0.4f, 0.86f, 0.1f), new Vector3(0.5f, 0.04f, 0.04f), Art.Hex("9AA3AD"));
        }

        static void Tires(MeshBuilder b, Vector3 at)
        {
            for (int i = 0; i < 4; i++)
                b.Cylinder(at + new Vector3(0f, i * 0.22f, 0f), 0.42f, 0.2f, 10, Art.Hex("26282C"), Art.Hex("3A3D42"));
        }

        static void Shelf(MeshBuilder b, Vector3 at)
        {
            foreach (var x in new[] { -1.2f, 1.2f })
                b.Box(at + new Vector3(x, 1.2f, 0f), new Vector3(0.08f, 2.4f, 0.5f), Beam);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.5f + i * 0.8f;
                b.Box(at + new Vector3(0f, y, 0f), new Vector3(2.5f, 0.06f, 0.5f), Art.Hex("59616C"));
                for (int k = 0; k < 4; k++)
                    b.Box(at + new Vector3(-0.9f + k * 0.6f, y + 0.15f, 0f), new Vector3(0.4f, 0.25f, 0.35f),
                        (i + k) % 3 == 0 ? Art.Hex("4F6B3A") : (i + k) % 3 == 1 ? Art.Hex("9C7A4A") : Art.Hex("4A5866"));
            }
        }

        // ---------------- Helpers ----------------

        static Color Glow(Color c, float amount = 1f) => new Color(c.r, c.g, c.b, 1f - amount);

        static Transform Part(string name, Mesh mesh, Transform parent, Vector3 position, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Models.Material;
            return go.transform;
        }

        void Lamp(Vector3 position, Color color, float range, float intensity)
        {
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }
}
