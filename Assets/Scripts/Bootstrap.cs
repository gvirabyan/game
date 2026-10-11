using UnityEngine;
using UnityEngine.Rendering;

namespace Corehold
{
    // The only object in the scene. Builds art, camera, light, game and UI at startup.
    public sealed class Bootstrap : MonoBehaviour
    {
        // Level layouts in balance.json are authored for this map width (see CameraRig.PlayArea).
        public const float WorldHalfWidth = 8.5f;

        static readonly Color Haze = Art.Hex("BCD3C8");

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowDistance = 60f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Art.Build();
            Models.Build();

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Haze;
            CameraRig.Fit(cam);

            // Warm low sun, cool sky fill, soft shadows and distance haze.
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Art.Hex("FFE7C4");
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.2f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Art.Hex("6E84A3");
            RenderSettings.ambientEquatorColor = Art.Hex("65705A");
            RenderSettings.ambientGroundColor = Art.Hex("3A3528");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;

            var game = new GameObject("Game").AddComponent<Game>();
            game.Init(cam);
            GameUI.Create(game);
        }
    }
}
