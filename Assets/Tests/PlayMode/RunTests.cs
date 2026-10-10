using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Corehold.Tests
{
    // Plays a full run in the real scene, sped up, and drives it through the real UI buttons.
    public class RunTests
    {
        const float TimeScale = 20f;
        const float TimeoutSeconds = 180f;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator FullRun_WavesShootUpgradeDieRestart()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var game = Game.Instance;
            Assert.NotNull(game, "Bootstrap did not create the Game");
            Assert.NotNull(Camera.main, "Bootstrap did not create a camera");
            Assert.AreEqual(RunState.Playing, game.State);
            Assert.AreEqual(1, game.Wave);

            Time.timeScale = TimeScale;

            // Waves arrive, the tower shoots and earns coins.
            yield return WaitUntil(() => game.Wave >= 2 && game.Kills > 0, "reach wave 2 with kills");
            Assert.Greater(game.ShotsFired, 0, "tower never fired");
            Assert.Greater(game.Coins, 0f, "kills gave no coins");
            Debug.Log($"[RunTests] wave {game.Wave}, kills {game.Kills}, shots {game.ShotsFired}, coins {game.Coins}");

            // Buy each of the four upgrades through its button.
            game.AddCoins(10000f);
            for (int i = 0; i < 4; i++)
            {
                var kind = (UpgradeKind)i;
                float before = game.StatValue(kind);
                Click("Upgrade_" + kind);
                Assert.AreEqual(1, game.UpgradeLevel(kind), $"{kind} button did not buy");
                Assert.Greater(game.StatValue(kind), before, $"{kind} stat did not grow");
            }

            // Enemies outscale the tower; the run must end.
            yield return WaitUntil(() => game.State == RunState.GameOver, "tower to die");
            Assert.LessOrEqual(game.TowerHealth, 0f);
            Assert.IsTrue(GameObject.Find("ResultPanel") != null, "result screen not shown");
            Debug.Log($"[RunTests] died on wave {game.Wave} after {game.RunTime:0}s, kills {game.Kills}");

            // Restart resets the run.
            Click("RestartButton");
            Assert.AreEqual(RunState.Playing, game.State);
            Assert.AreEqual(1, game.Wave);
            Assert.AreEqual(0, game.Kills);
            Assert.AreEqual(0, game.EnemyCount);
            Assert.AreEqual(0, game.UpgradeLevel(UpgradeKind.Damage));
            Assert.AreEqual(game.MaxHealth, game.TowerHealth);
            Assert.IsTrue(GameObject.Find("ResultPanel") == null, "result screen still shown");
        }

        [UnityTest]
        public IEnumerator StressMode_Holds200EnemiesWithoutPoolGrowth()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var game = Game.Instance;
            Click("StressButton");
            Assert.IsTrue(game.StressMode);

            for (int i = 0; i < 120; i++) yield return null;
            Assert.AreEqual(Game.StressEnemyCount, game.EnemyCount);
            Assert.AreEqual(RunState.Playing, game.State, "tower should not take damage in stress mode");
            Assert.AreEqual(0, game.PoolGrowth, "pools had to allocate during play");
        }

        static void Click(string name)
        {
            var go = GameObject.Find(name);
            Assert.NotNull(go, $"UI object '{name}' not found");
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
