using UnityEngine;

namespace Corehold
{
    // The only object in the scene. Builds the art atlas, camera, game and UI at startup.
    public sealed class Bootstrap : MonoBehaviour
    {
        // Level layouts in balance.json are authored for this visible area (world units).
        public const float WorldHalfWidth = 5f;
        public const float MinHalfHeight = 8.9f;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Art.Build();

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Palette.Ground;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            // Fixed world width on phones; wide screens (tablets) keep the full level height instead.
            cam.orthographicSize = Mathf.Max(WorldHalfWidth / cam.aspect, MinHalfHeight);

            var game = new GameObject("Game").AddComponent<Game>();
            game.Init(cam);
            GameUI.Create(game);
        }
    }
}
