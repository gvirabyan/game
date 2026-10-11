using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Corehold.Tests
{
    // Renders the game to PNG files in Logs/Screens so visuals can be checked without a phone.
    // Explicit: run with -testFilter Corehold.Tests.ScreenshotTests (needs graphics, so no -nographics).
    [Explicit, Category("Screenshots")]
    public class ScreenshotTests
    {
        const int Width = 1600, Height = 720;

        [UnityTest]
        public IEnumerator CaptureMenuAndLevels()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var game = Game.Instance;
            // Match the phone's aspect ratio before anything is laid out.
            var cam = Camera.main;
            cam.aspect = Width / (float)Height;
            cam.orthographicSize = Mathf.Max(Bootstrap.WorldHalfWidth / cam.aspect, Bootstrap.MinHalfHeight);
            game.ShowMenu();
            yield return null;
            Capture("menu");

            for (int level = 0; level < game.Balance.levels.Length; level++)
            {
                game.LoadLevel(level);
                game.AddCoins(10000);
                for (int i = 0; i < game.Spots.Count; i += 2)
                {
                    game.Select(i);
                    game.TryBuild(i % 3 == 0 ? "mg" : i % 3 == 1 ? "cannon" : "aa");
                }
                game.Select(0);
                game.StartNextWave();
                Time.timeScale = 4f;
                for (int f = 0; f < 120; f++) yield return null;
                Time.timeScale = 1f;
                Capture("level" + (level + 1));
            }
        }

        // Renders the main camera and every overlay canvas (switched to camera space) into one image.
        static void Capture(string name)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(Width, Height, 24) { antiAliasing = 2 };
            var canvases = Object.FindObjectsByType<Canvas>();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 5f;
            }
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;

            Directory.CreateDirectory("Logs/Screens");
            File.WriteAllBytes($"Logs/Screens/{name}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
