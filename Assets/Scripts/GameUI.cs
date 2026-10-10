using UnityEngine;
using UnityEngine.UI;

namespace Corehold
{
    // HUD, upgrade buttons, result screen and debug overlay, all built from code.
    public sealed class GameUI : MonoBehaviour
    {
        const float RefreshInterval = 0.1f;

        static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
        static readonly Color Affordable = new Color(0.16f, 0.45f, 0.72f);
        static readonly Color TooExpensive = new Color(0.2f, 0.22f, 0.27f);
        static readonly Color Maxed = new Color(0.12f, 0.13f, 0.16f);
        static readonly Color HealthBack = new Color(0.25f, 0.08f, 0.08f);
        static readonly Color HealthFill = new Color(0.3f, 0.85f, 0.45f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        Game game;
        Text coinsText, waveText, healthText, resultText, debugText, stressLabel;
        RectTransform healthFill;
        GameObject resultPanel;
        readonly Button[] upgradeButtons = new Button[4];
        readonly Text[] upgradeLabels = new Text[4];
        float refreshTimer;

        // Debug overlay state
        int fpsFrames;
        float fpsTime;
        float fps, worstMs, worstMsWindow;

        public static GameUI Create(Game game)
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("UI", 10);
            var ui = canvas.gameObject.AddComponent<GameUI>();
            ui.Build(game, canvas.transform);
            return ui;
        }

        void Build(Game g, Transform canvas)
        {
            game = g;
            var safe = UIFactory.Fill("SafeArea", canvas);
            safe.gameObject.AddComponent<SafeArea>();

            BuildTopBar(safe);
            BuildDebugOverlay(safe);
            BuildUpgradePanel(safe);
            BuildResultPanel(canvas);

            game.RunEnded += ShowResult;
            game.RunStarted += () => resultPanel.SetActive(false);
            Refresh();
        }

        void BuildTopBar(RectTransform root)
        {
            var bar = UIFactory.Rect("TopBar", root, new Vector2(0, 1), Vector2.one, new Vector2(0, -190), Vector2.zero);
            UIFactory.Image(bar, PanelColor);

            coinsText = UIFactory.Text(UIFactory.Rect("Coins", bar, new Vector2(0, 0.45f), new Vector2(0.6f, 1), new Vector2(40, 0), Vector2.zero),
                "", 64, TextAnchor.MiddleLeft, Gold);
            waveText = UIFactory.Text(UIFactory.Rect("Wave", bar, new Vector2(0.5f, 0.45f), new Vector2(1, 1), Vector2.zero, new Vector2(-40, 0)),
                "", 56, TextAnchor.MiddleRight, Color.white);

            var back = UIFactory.Rect("HealthBar", bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 24), new Vector2(-40, 80));
            UIFactory.Image(back, HealthBack);
            healthFill = UIFactory.Fill("Fill", back);
            UIFactory.Image(healthFill, HealthFill);
            healthText = UIFactory.Text(UIFactory.Fill("HealthText", back), "", 36, TextAnchor.MiddleCenter, Color.white);
        }

        void BuildDebugOverlay(RectTransform root)
        {
            var area = UIFactory.Rect("Debug", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -420), new Vector2(-24, -200));
            debugText = UIFactory.Text(UIFactory.Rect("DebugText", area, Vector2.zero, new Vector2(0.6f, 1), Vector2.zero, Vector2.zero),
                "", 30, TextAnchor.UpperLeft, new Color(0.7f, 1f, 0.7f));

            var stress = UIFactory.Rect("StressButton", area, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -90), Vector2.zero);
            var button = UIFactory.Button(stress, new Color(0.3f, 0.3f, 0.3f, 0.7f), out stressLabel, "", 30);
            button.onClick.AddListener(() =>
            {
                game.StressMode = !game.StressMode;
                Refresh();
            });
        }

        void BuildUpgradePanel(RectTransform root)
        {
            var panel = UIFactory.Rect("Upgrades", root, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 440));
            UIFactory.Image(panel, PanelColor, true);   // blocks taps from reaching the field

            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = 1 - i / 2;
                var rt = UIFactory.Rect("Upgrade_" + (UpgradeKind)i, panel,
                    new Vector2(col * 0.5f, row * 0.5f), new Vector2(col * 0.5f + 0.5f, row * 0.5f + 0.5f),
                    new Vector2(col == 0 ? 30 : 15, 15), new Vector2(col == 0 ? -15 : -30, -15));
                var kind = (UpgradeKind)i;
                upgradeButtons[i] = UIFactory.Button(rt, Affordable, out upgradeLabels[i], "", 34);
                upgradeButtons[i].onClick.AddListener(() =>
                {
                    if (game.TryBuy(kind)) Refresh();
                });
            }
        }

        void BuildResultPanel(Transform canvas)
        {
            var panel = UIFactory.Fill("ResultPanel", canvas);
            UIFactory.Image(panel, new Color(0, 0, 0, 0.8f), true);
            resultPanel = panel.gameObject;

            UIFactory.Text(UIFactory.Rect("Title", panel, new Vector2(0, 0.6f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero),
                "Tower destroyed", 96, TextAnchor.MiddleCenter, Color.white);
            resultText = UIFactory.Text(UIFactory.Rect("Stats", panel, new Vector2(0, 0.42f), new Vector2(1, 0.6f), Vector2.zero, Vector2.zero),
                "", 56, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.9f));

            var rt = UIFactory.Rect("RestartButton", panel, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(-300, -90), new Vector2(300, 90));
            var restart = UIFactory.Button(rt, Affordable, out _, "Restart", 64);
            restart.onClick.AddListener(game.StartRun);

            resultPanel.SetActive(false);
        }

        void ShowResult()
        {
            int seconds = Mathf.FloorToInt(game.RunTime);
            resultText.text = $"Wave {game.Wave}\nKills {game.Kills}\nTime {seconds / 60}:{seconds % 60:00}";
            resultPanel.SetActive(true);
            Refresh();
        }

        void Update()
        {
            UpdateFps();
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= RefreshInterval) Refresh();
        }

        void Refresh()
        {
            refreshTimer = 0f;
            coinsText.text = "Coins " + UIFactory.Number(game.Coins);
            waveText.text = "Wave " + game.Wave;

            float max = game.MaxHealth;
            float hp = Mathf.Max(0f, game.TowerHealth);
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1f);
            healthText.text = UIFactory.Number(Mathf.Ceil(hp)) + " / " + UIFactory.Number(max);

            for (int i = 0; i < 4; i++) RefreshUpgrade((UpgradeKind)i);

            stressLabel.text = game.StressMode ? "Stress: ON" : "Stress: off";
            debugText.text =
                $"FPS {fps:0}  (worst {worstMs:0.0} ms)\n" +
                $"Enemies {game.EnemyCount}\n" +
                $"Wave {game.Wave}\n" +
                $"Projectiles {game.ProjectileCount}" +
                (game.PoolGrowth > 0 ? $"\nPool grew +{game.PoolGrowth}" : "");
        }

        void RefreshUpgrade(UpgradeKind kind)
        {
            int i = (int)kind;
            var info = game.UpgradeInfo(kind);
            string value = UIFactory.Number(game.StatValue(kind));
            string line2, line3;
            Color color;
            if (game.IsMaxed(kind))
            {
                line2 = $"Lv {game.UpgradeLevel(kind)}  {value}";
                line3 = "MAX";
                color = Maxed;
            }
            else
            {
                line2 = $"Lv {game.UpgradeLevel(kind)}  {value} → {UIFactory.Number(game.NextStatValue(kind))}";
                line3 = UIFactory.Number(game.UpgradeCost(kind)) + " coins";
                color = game.CanBuy(kind) ? Affordable : TooExpensive;
            }
            upgradeLabels[i].text = $"<b><size=44>{info.label}</size></b>\n{line2}\n<color=#FFD95A>{line3}</color>";
            upgradeButtons[i].image.color = color;
        }

        void UpdateFps()
        {
            float dt = Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTime += dt;
            worstMsWindow = Mathf.Max(worstMsWindow, dt * 1000f);
            if (fpsTime >= 0.5f)
            {
                fps = fpsFrames / fpsTime;
                worstMs = worstMsWindow;
                fpsFrames = 0;
                fpsTime = 0f;
                worstMsWindow = 0f;
            }
        }
    }
}
