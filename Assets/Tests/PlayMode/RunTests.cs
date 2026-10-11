using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Corehold.Tests
{
    // Plays real levels in the real scene, sped up, driving them through the real UI buttons.
    public class RunTests
    {
        const float TimeScale = 20f;
        const float TimeoutSeconds = 240f;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.NotNull(Game.Instance, "Bootstrap did not create the Game");
            Assert.AreEqual(GameState.Menu, Game.Instance.State);
        }

        [UnityTest]
        public IEnumerator Undefended_LevelFails_ThenRetryResets()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("Level_1");
            Assert.AreEqual(GameState.Playing, game.State);
            int startCoins = game.Coins;

            Click("WaveButton");
            Assert.AreEqual(1, game.WavesStarted);
            Time.timeScale = TimeScale;

            yield return WaitUntil(() => game.State == GameState.Lost, "level to be lost");
            Assert.AreEqual(0, game.Lives);
            Assert.IsTrue(GameObject.Find("ResultPanel") != null, "result screen not shown");
            Debug.Log($"[RunTests] undefended: lost on wave {game.WavesStarted}");

            Click("RetryButton");
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(0, game.WavesStarted);
            Assert.AreEqual(startCoins, game.Coins);
            Assert.AreEqual(game.Level.lives, game.Lives);
            Assert.AreEqual(0, game.EnemyCount);
        }

        [UnityTest]
        public IEnumerator FirstWave_StartsOnItsOwn()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("Level_1");
            Assert.AreEqual(0, game.WavesStarted);
            float delay = game.Balance.economy.firstWaveDelay;
            Assert.Greater(delay, 0f);
            Time.timeScale = TimeScale;
            float start = Time.time;
            yield return WaitUntil(() => game.WavesStarted == 1, "the first wave to start without a tap");
            Assert.AreEqual(delay, Time.time - start, 0.5f, "first wave started at the wrong time");
        }

        [UnityTest]
        public IEnumerator BuildUpgradeSell_ThroughPanels()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("Level_1");
            game.AddCoins(1000);

            game.Select(0);
            Assert.IsTrue(GameObject.Find("BuildPanel") != null, "build panel not shown");
            int before = game.Coins;
            Click("Build_mg");
            Assert.NotNull(game.Spots[0].tower, "tower not built");
            Assert.AreEqual(before - game.Balance.Tower("mg").cost, game.Coins);
            Assert.IsTrue(GameObject.Find("TowerPanel") != null, "tower panel not shown after building");

            for (int i = 0; i < 3; i++) Click("UpgradeButton");
            Assert.AreEqual(3, game.Spots[0].tower.level);
            Assert.IsFalse(game.TryUpgrade(), "upgraded past max level");

            int spent = game.Spots[0].tower.spent;
            before = game.Coins;
            Click("SellButton");
            Assert.IsNull(game.Spots[0].tower, "tower not sold");
            Assert.AreEqual(before + Formulas.SellValue(spent, game.Balance.economy), game.Coins);
        }

        [UnityTest]
        public IEnumerator DefendedLevels_AreWonAndAdvance()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("Level_1");

            for (int level = 0; level < game.Balance.levels.Length; level++)
            {
                Assert.AreEqual(level, game.LevelIndex);
                FortifyAllSpots(game);
                Click("WaveButton");
                Time.timeScale = TimeScale;
                yield return WaitUntil(() => game.State != GameState.Playing, $"level {level + 1} to finish");
                Assert.AreEqual(GameState.Won, game.State, $"level {level + 1} lost on wave {game.WavesStarted}");
                Debug.Log($"[RunTests] level {level + 1} won, kills {game.Kills}, lives {game.Lives}/{game.Level.lives}");
                if (level < game.Balance.levels.Length - 1) Click("NextLevelButton");
            }
        }

        // Build a mix of towers on every pad, fully upgraded: mostly mg, a cannon and missiles where drones appear.
        static void FortifyAllSpots(Game game)
        {
            game.AddCoins(100000);
            for (int i = 0; i < game.Spots.Count; i++)
            {
                game.Select(i);
                Click(i % 3 == 0 ? "Build_aa" : i % 3 == 1 ? "Build_cannon" : "Build_mg");
                for (int u = 0; u < 3; u++) Click("UpgradeButton");
            }
            game.Deselect();
        }

        [UnityTest]
        public IEnumerator StressMode_Holds200MovingEnemiesWithoutPoolGrowth()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("Level_1");
            Click("StressButton");
            Assert.IsTrue(game.StressMode);

            for (int i = 0; i < 300; i++) yield return null;
            Assert.AreEqual(Game.StressEnemyCount, game.EnemyCount);
            Assert.AreEqual(game.Level.lives, game.Lives, "stress mode must not cost lives");
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(0, game.PoolGrowth, "pools had to allocate during play");
        }

        static void Click(string name)
        {
            var go = GameObject.Find(name);
            Assert.NotNull(go, $"UI object '{name}' not found or not active");
            go.GetComponent<Button>().onClick.Invoke();
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > TimeoutSeconds)
                    Assert.Fail($"Timed out waiting for {what}");
                yield return null;
            }
        }
    }
}
