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
            GameObject.Find("Level_1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Capture("level_info");
            GameObject.Find("Close").GetComponent<Button>().onClick.Invoke();
            foreach (var code in new[] { "hy", "ru", "zh" })
            {
                Loc.Set(code);
                yield return null;
                yield return null;
                Capture("menu_" + code);
            }
            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Capture("settings");
            Loc.Set("en");
            yield return null;
            Progress.ResetAll();

            var picks = new[] { 0, 30, 55, 80 };
            for (int level = 0; level < picks.Length; level++)
            {
                game.LoadLevel(picks[level]);
                var ok = GameObject.Find("IntroOk");
                if (ok != null) ok.GetComponent<Button>().onClick.Invoke();
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

        [UnityTest]
        public IEnumerator CaptureIntroCard()
        {
            Progress.ResetAll();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var cam = Camera.main;
            cam.aspect = Width / (float)Height;
            Game.Instance.LoadLevel(4);
            yield return null;
            Capture("intro");
            Time.timeScale = 1f;
        }

        // App Store / Google Play screenshots in Store/screenshots/<set>/NN.png (iPhone 6.7" and 16:9 for Play).
        [UnityTest]
        public IEnumerator CaptureStoreScreenshots()
        {
            foreach (var (set, w, h) in new[] { ("iphone-6.7", 2796, 1290), ("iphone-6.1", 2556, 1179), ("play-16x9", 1920, 1080) })
            {
                Progress.ResetAll();
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
                var game = Game.Instance;
                // Lay the UI out at the target resolution for the whole run (not the batch window's size).
                var cam = Camera.main;
                var target = new RenderTexture(w, h, 24) { antiAliasing = 2 };
                cam.targetTexture = target;
                foreach (var c in Object.FindObjectsByType<Canvas>())
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 5f;
                }
                game.ShowMenu();
                yield return null;
                yield return null;
                string dir = "Store/screenshots/" + set;
                StoreCapture("01", target, dir);

                var picks = new[] { 2, 33, 58, 84 };
                for (int n = 0; n < picks.Length; n++)
                {
                    game.LoadLevel(picks[n]);
                    var ok = GameObject.Find("IntroOk");
                    if (ok != null) ok.GetComponent<Button>().onClick.Invoke();
                    game.AddCoins(20000);
                    var towers = game.Balance.towers;
                    for (int i = 0; i < game.Spots.Count; i++)
                    {
                        if (i % 3 == 2) continue; // leave a few pads free
                        game.Select(i);
                        for (int k = 0; k < towers.Length; k++)
                            if (game.TryBuild(towers[(i + k + n * 3) % towers.Length].id)) break;
                        for (int u = 0; u < (i + n) % 4; u++) game.TryUpgrade();
                    }
                    game.Deselect();
                    game.AddCoins(-(game.Coins - (340 + n * 115))); // a believable purse in the HUD
                    game.StartNextWave();
                    Time.timeScale = 2f;
                    for (int f = 0; f < 100 + n * 15; f++) yield return null;
                    if (game.CanStartNextWave) game.StartNextWave();
                    for (int f = 0; f < 40; f++) yield return null;
                    Time.timeScale = 1f;
                    if (n == 1) game.Select(1);
                    yield return null;
                    StoreCapture((n + 2).ToString("00"), target, dir);
                }
                cam.targetTexture = null;
                target.Release();
            }
            Progress.ResetAll();
        }

        // Renders the camera (already targeting `rt`, canvases in camera space) without the debug overlay.
        static void StoreCapture(string name, RenderTexture rt, string dir)
        {
            var debug = GameObject.Find("Debug");
            if (debug != null) debug.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Camera.main.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            if (debug != null) debug.SetActive(true);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static void Capture(string name) => Capture(name, Width, Height, "Logs/Screens");

        // Renders the main camera and every overlay canvas (switched to camera space) into one image.
        static void Capture(string name, int width, int height, string dir)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 2 };
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
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
