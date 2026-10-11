using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Corehold
{
    // UI icons rendered once at startup from the real 3D models, so cards show exactly what gets built.
    // Without a graphics device (batch tests with -nographics) the 2D atlas sprites are used instead.
    public static class Icons
    {
        const int Size = 192;
        const int Layer = 31;

        static readonly Dictionary<string, Sprite> towers = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> enemies = new Dictionary<string, Sprite>();
        static bool built;

        public static Sprite Tower(string id, int level) =>
            towers.TryGetValue(id + level, out var s) ? s : Art.TowerBase;

        public static Sprite Enemy(string id) =>
            enemies.TryGetValue(id, out var s) ? s : Art.Coin;

        public static void Build(BalanceData balance)
        {
            if (built) return;
            built = true;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

            var rig = new GameObject("IconRig");
            rig.transform.position = new Vector3(0f, -500f, 0f);
            var cam = new GameObject("IconCamera").AddComponent<Camera>();
            cam.transform.SetParent(rig.transform, false);
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.enabled = false;
            cam.transform.localRotation = Quaternion.Euler(32f, -35f, 0f);
            cam.transform.localPosition = cam.transform.localRotation * new Vector3(0f, 0.45f, -8f);
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;

            var model = new GameObject("Model").transform;
            model.SetParent(rig.transform, false);

            foreach (var t in balance.towers)
                for (int level = 0; level < Models.TowerLevels; level++)
                {
                    Clear(model);
                    Part(model, Models.TowerBase(level), Vector3.zero, Quaternion.identity);
                    Part(model, Models.Turret(t.id, level), new Vector3(0f, 0.32f, 0f), Quaternion.Euler(0f, -20f, 0f));
                    cam.orthographicSize = 0.62f;
                    towers[t.id + level] = Capture(cam, rt, t.id + level);
                }
            foreach (var e in balance.enemies)
            {
                Clear(model);
                var unit = Models.Rig(e.id);
                Part(model, unit.Body, Vector3.zero, Quaternion.identity);
                for (int i = 0; i < unit.Parts.Length; i++) Part(model, unit.Parts[i], unit.Joints[i], Quaternion.identity);
                model.localRotation = Quaternion.Euler(0f, -25f, 0f);
                cam.orthographicSize = e.id == "soldier" ? 0.75f : e.id == "heli" || e.id == "boss" ? 0.85f : 0.7f;
                cam.transform.localPosition = cam.transform.localRotation * new Vector3(0f, 0f, -8f) + new Vector3(0f, e.id == "soldier" ? 0.6f : 0.3f, 0f);
                enemies[e.id] = Capture(cam, rt, e.id);
                model.localRotation = Quaternion.identity;
            }

            RenderSettings.fog = fog;
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rig);
        }

        static void Clear(Transform model)
        {
            for (int i = model.childCount - 1; i >= 0; i--) Object.DestroyImmediate(model.GetChild(i).gameObject);
        }

        static void Part(Transform parent, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Part") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Models.Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Sprite Capture(Camera cam, RenderTexture rt, string name)
        {
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply(false, true);
            RenderTexture.active = previous;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
