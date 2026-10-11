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
            game.ShowMenu();
            yield return null;
            Capture("menu");
            GameObject.Find("Level_6").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Capture("level_info");

            for (int level = 0; level < 4; level++)
            {
                game.LoadLevel(level);
                game.AddCoins(10000);
                for (int i = 0; i < game.Spots.Count; i += 2)
                {
                    game.Select(i);
                    var towers = game.Balance.towers;
                    game.TryBuild(towers[(i / 2 + level * 3) % towers.Length].id);
                    for (int u = 0; u < (i / 2) % 4; u++) game.TryUpgrade();
                }
                game.Select(0);
                game.StartNextWave();
                Time.timeScale = 4f;
                for (int f = 0; f < 120; f++) yield return null;
                Time.timeScale = 1f;
                Capture("level" + (level + 1));
                if (level == 1)
                {
                    game.Select(1);
                    yield return null;
                    Capture("build_panel");
                }
                if (level == 0)
                {
                    game.Deselect();
                    game.Zoom = 2f;
                    yield return null;
                    yield return null;
                    Capture("level1_zoom");
                }
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
