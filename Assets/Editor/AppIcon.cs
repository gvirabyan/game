using System.IO;
using Corehold;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Generates the app icon and store graphics from the game's own low-poly models (no external art) and
// assigns the icons in Player Settings. Needs a graphics device, so run WITHOUT -nographics:
//   Unity.exe -batchmode -quit -buildTarget Android -projectPath . -executeMethod AppIcon.Generate -logFile -
// Writes Assets/Icons/*.png (used by builds) and Store/*.png (uploaded by hand to the stores).
public static class AppIcon
{
    const string IconDir = "Assets/Icons";
    const string StoreDir = "Store";
    const int Layer = 31;

    static readonly Color SkyTop = Art.Hex("2E6E8E"), SkyBottom = Art.Hex("0E2433");
    static readonly Color Glow = Art.Hex("FFC266");
    // Composition (map units of the game's models).
    const float TurretYaw = 187f, TankYaw = 7f, TankScale = 0.6f, CamSize = 0.95f;
    static readonly Vector3 TankPos = new Vector3(-1.2f, 0f, 0.15f), CamTarget = new Vector3(-0.45f, 0.3f, 0.05f);

    public static void Generate()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Art.Build();
        Models.Build();
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Art.Hex("8FA6C4");
        RenderSettings.ambientEquatorColor = Art.Hex("6E7A62");
        RenderSettings.ambientGroundColor = Art.Hex("3A3528");
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Art.Hex("FFE7C4");
        light.intensity = 1.05f;
        light.transform.rotation = Quaternion.Euler(40f, -60f, 0f);

        // Foreground: the top-level cannon tower guarding against a tank and a soldier.
        var root = new GameObject("IconModels").transform;
        int top = Models.TowerLevels - 1;
        Part(root, Models.TowerBase(top), Vector3.zero, Quaternion.identity);
        Part(root, Models.Turret("cannon", top), new Vector3(0f, 0.32f, 0f), Quaternion.Euler(0f, TurretYaw, 0f));
        Unit(root, "tank", TankPos, Quaternion.Euler(0f, TankYaw, 0f), TankScale);

        var cam = new GameObject("IconCamera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << Layer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 30f;
        cam.transform.rotation = Quaternion.Euler(30f, -35f, 0f);
        cam.transform.position = CamTarget + cam.transform.rotation * new Vector3(0f, 0f, -10f);
        cam.orthographicSize = CamSize;

        var fg = Render(cam, 2048, 2048, 1024, 1024);
        Directory.CreateDirectory(IconDir);
        Directory.CreateDirectory(StoreDir);

        // Full icon (iOS, Play listing, legacy Android): opaque, the models fill ~85% of the square.
        var icon = Compose(1024, 1024, fg, 0.88f, true);
        Save(icon, IconDir + "/AppIcon.png");
        Save(Resize(icon, 512, 512), StoreDir + "/play-icon-512.png");
        // Android adaptive icon layers (432 px, launcher masks to a circle/squircle: keep content in the middle 66%).
        Save(Compose(432, 432, null, 0f, true), IconDir + "/AdaptiveBackground.png");
        Save(Compose(432, 432, fg, 0.62f, false), IconDir + "/AdaptiveForeground.png");
        // Google Play feature graphic 1024x500.
        Save(Compose(1024, 500, fg, 0.96f, true), StoreDir + "/play-feature-1024x500.png");

        Object.DestroyImmediate(fg);
        AssetDatabase.Refresh();
        Assign();
        AssetDatabase.SaveAssets();
        Debug.Log("[AppIcon] Done");
    }

    static void Unit(Transform root, string id, Vector3 position, Quaternion rotation, float scale)
    {
        var unit = Models.Rig(id);
        var t = new GameObject(id).transform;
        t.SetParent(root, false);
        t.localPosition = position;
        t.localRotation = rotation;
        t.localScale = Vector3.one * scale;
        Part(t, unit.Body, Vector3.zero, Quaternion.identity);
        for (int i = 0; i < unit.Parts.Length; i++) Part(t, unit.Parts[i], unit.Joints[i], Quaternion.identity);
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
    }

    // Renders at high resolution and downsamples for smooth edges.
    static Texture2D Render(Camera cam, int w, int h, int outW, int outH)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.aspect = w / (float)h;
        cam.Render();
        RenderTexture.active = rt;
        var big = new Texture2D(w, h, TextureFormat.RGBA32, false);
        big.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        big.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
        var small = Resize(big, outW, outH);
        Object.DestroyImmediate(big);
        return small;
    }

    // Background gradient with a warm glow, then the foreground (square) centred at `scale` of the height.
    static Texture2D Compose(int w, int h, Texture2D fg, float scale, bool background)
    {
        var tex = new Texture2D(w, h, background ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        float size = h * scale, x0 = (w - size) * 0.5f, y0 = (h - size) * 0.42f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                if (background)
                {
                    float v = y / (float)(h - 1);
                    c = Color.Lerp(SkyBottom, SkyTop, v);
                    float dx = (x - w * 0.5f) / h, dy = (y - h * 0.55f) / h;
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.48f);
                    c = Color.Lerp(c, Glow, glow * glow * 0.75f);
                    c.a = 1f;
                }
                if (fg != null)
                {
                    float u = (x - x0) / size, t = (y - y0) / size;
                    if (u > 0.003f && u < 0.997f && t > 0.003f && t < 0.997f) // skip the clamped border texels
                    {
                        var f = fg.GetPixelBilinear(u, t);
                        c = background ? new Color(Mathf.Lerp(c.r, f.r, f.a), Mathf.Lerp(c.g, f.g, f.a), Mathf.Lerp(c.b, f.b, f.a), 1f) : f;
                    }
                }
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D Resize(Texture2D src, int w, int h)
    {
        var dst = new Texture2D(w, h, src.format == TextureFormat.RGB24 ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        // Box filter: average the source pixels covered by each destination pixel.
        int sx = Mathf.Max(1, src.width / w), sy = Mathf.Max(1, src.height / h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int j = 0; j < sy; j++)
                    for (int i = 0; i < sx; i++)
                        sum += src.GetPixelBilinear((x * sx + i + 0.5f) / src.width, (y * sy + j + 0.5f) / src.height);
                var c = sum / (sx * sy);
                // Un-premultiply so semi-transparent edges keep their colour.
                if (c.a > 0f && c.a < 1f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; }
                px[y * w + x] = c;
            }
        dst.SetPixels(px);
        dst.Apply();
        return dst;
    }

    static void Save(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG()); // one-off editor run: textures are left to the editor to unload
    }

    static Texture2D Import(string path, bool alpha)
    {
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = alpha;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Assign()
    {
        var icon = Import(IconDir + "/AppIcon.png", false);
        var bg = Import(IconDir + "/AdaptiveBackground.png", false);
        var fg = Import(IconDir + "/AdaptiveForeground.png", true);

        // Default icon: Unity scales it to every iOS size, including the 1024 App Store icon.
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        // Android: adaptive icon only (legacy/round kinds are obsolete in this Unity version).
        var android = NamedBuildTarget.Android;
        var icons = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
        foreach (var i in icons) i.SetTextures(bg, fg);
        PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, icons);
    }
}
