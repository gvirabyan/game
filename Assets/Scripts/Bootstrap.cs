using UnityEngine;

namespace Corehold
{
    // The only object in the scene. Builds the camera, the game and the UI at startup.
    public sealed class Bootstrap : MonoBehaviour
    {
        static readonly Color Background = new Color(0.07f, 0.08f, 0.11f);

        Camera cam;
        float halfWidth;
        float lastAspect;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var game = new GameObject("Game").AddComponent<Game>();
            game.Init(cam);
            halfWidth = game.Balance.run.worldHalfWidth;
            FitCamera();

            GameUI.Create(game);
        }

        void Update()
        {
            if (!Mathf.Approximately(cam.aspect, lastAspect)) FitCamera();
        }

        // Keep a fixed world width on every phone; taller phones just see more vertically.
        void FitCamera()
        {
            lastAspect = cam.aspect;
            cam.orthographicSize = halfWidth / cam.aspect;
        }
    }
}
