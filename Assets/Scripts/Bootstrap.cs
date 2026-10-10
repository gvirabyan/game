using UnityEngine;
using UnityEngine.Rendering;

namespace Corehold
{
    // The only object in the scene. Builds art, camera, light, game and UI at startup.
    public sealed class Bootstrap : MonoBehaviour
    {
        // Level layouts in balance.json are authored for this visible area (map units).
        public const float WorldHalfWidth = 5f;
        public const float MinHalfHeight = 8.9f;

        static readonly Color Sky = Art.Hex("6F9A55");

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowDistance = 120f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Art.Build();
            Models.Build();

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 120f;
            // Fixed map width on phones; wide screens (tablets) keep the full level height instead.
            cam.orthographicSize = Mathf.Max(WorldHalfWidth / cam.aspect, MinHalfHeight);
            cam.transform.rotation = Quaternion.Euler(View.Pitch, 0f, 0f);
            cam.transform.position = -(cam.transform.rotation * Vector3.forward) * 50f;

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Art.Hex("FFF4E0");
            light.intensity = 0.8f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.7f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.1f;
            light.transform.rotation = Quaternion.Euler(42f, -50f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Art.Hex("7D8A99");
            RenderSettings.ambientEquatorColor = Art.Hex("6A7466");
            RenderSettings.ambientGroundColor = Art.Hex("3A4034");

            var game = new GameObject("Game").AddComponent<Game>();
            game.Init(cam);
            GameUI.Create(game);
        }
    }
}
