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
            OpenLevel(1);
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
            OpenLevel(1);
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
            OpenLevel(1);
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
            OpenLevel(1);

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

        // Every tower type in turn on every pad, fully upgraded, so each attack kind gets exercised.
        static void FortifyAllSpots(Game game)
        {
            game.AddCoins(100000);
            var towers = game.Balance.towers;
            for (int i = 0; i < game.Spots.Count; i++)
            {
                game.Select(i);
                Click("Build_" + towers[i % towers.Length].id);
                for (int u = 0; u < 3; u++) Click("UpgradeButton");
            }
            game.Deselect();
        }

        static void OpenLevel(int number)
        {
            Click("ModeLevels");
            Click("Level_" + number);
        }

        [UnityTest]
        public IEnumerator Endless_GeneratesHarderWavesUntilTheBaseFalls()
        {
            yield return LoadScene();
            var game = Game.Instance;
            Click("ModeEndless");
            Assert.IsTrue(game.Endless);
            Assert.AreEqual(GameState.Playing, game.State);
            FortifyAllSpots(game);
            Click("WaveButton");
            Time.timeScale = TimeScale;
            yield return WaitUntil(() => game.WavesStarted >= 6, "six endless waves");
            Assert.AreEqual(GameState.Playing, game.State, "a fully fortified base fell in the first waves");
            Debug.Log($"[RunTests] endless: reached wave {game.WavesStarted}, kills {game.Kills}, lives {game.Lives}");

            // Without towers the base must eventually fall, and the result screen offers a retry.
            game.Select(0);
            for (int i = 0; i < game.Spots.Count; i++) { game.Select(i); game.TrySell(); }
            yield return WaitUntil(() => game.State == GameState.Lost, "the undefended endless base to fall");
            Assert.IsTrue(game.EndlessBest >= 5);
            Click("RetryButton");
            Assert.IsTrue(game.Endless);
            Assert.AreEqual(0, game.WavesStarted);
        }

        [UnityTest]
        public IEnumerator StressMode_Holds200MovingEnemiesWithoutPoolGrowth()
        {
            yield return LoadScene();
            var game = Game.Instance;
            OpenLevel(1);
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
