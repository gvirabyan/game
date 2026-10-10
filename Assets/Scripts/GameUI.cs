using UnityEngine;
using UnityEngine.UI;

namespace Corehold
{
    // Menu, HUD, build/upgrade panels, pause, result screen and debug overlay, all built from code.
    // Object names (Level_1, WaveButton, Build_mg, UpgradeButton, ...) are used by the PlayMode tests.
    public sealed class GameUI : MonoBehaviour
    {
        const float RefreshInterval = 0.1f;
        const float Margin = 24f;
        static readonly string[] TowerIds = { "mg", "cannon", "aa" };
        static readonly string[] TowerRoles = { "Ground + air", "Ground, splash", "Air only" };

        Game game;
        GameObject menu, hud, bottomBar, buildPanel, towerPanel, resultPanel, pausePanel, debugRoot;
        Text coinsText, livesText, waveText, hintText, waveButtonLabel, speedLabel, debugText, stressLabel;
        Text towerTitle, towerStats, upgradeLabel, sellLabel, resultTitle, resultStats;
        Image waveButtonImage, upgradeImage, towerIconTurret;
        Button waveButton, nextLevelButton;
        readonly Button[] buildButtons = new Button[3];
        readonly Text[] buildCosts = new Text[3];
        float refreshTimer, speed = 1f;
        bool paused;

        int fpsFrames;
        float fpsTime, fps, worstMs, worstMsWindow;

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

            BuildHud(safe);
            BuildDebug(safe);
            BuildBottomBar(safe);
            BuildBuildPanel(safe);
            BuildTowerPanel(safe);
            BuildPausePanel(canvas);
            BuildResultPanel(canvas);
            BuildMenu(canvas);

            game.StateChanged += OnStateChanged;
            game.SelectionChanged += Refresh;
            OnStateChanged();
        }

        // ---------------- Layout ----------------

        void BuildHud(RectTransform root)
        {
            var bar = UIFactory.Rect("Hud", root, new Vector2(0, 1), Vector2.one, new Vector2(Margin, -Margin - 130), new Vector2(-Margin, -Margin));
            UIFactory.Image(bar, UIFactory.PanelColor);
            hud = bar.gameObject;

            UIFactory.Icon(UIFactory.Box("CoinIcon", bar, new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(64, 64)), Art.Coin);
            coinsText = UIFactory.Text(UIFactory.Box("Coins", bar, new Vector2(0, 0.5f), new Vector2(104, 0), new Vector2(220, 80)),
                "", 56, TextAnchor.MiddleLeft, Art.Palette.Gold, true);
            UIFactory.Icon(UIFactory.Box("HeartIcon", bar, new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(60, 60)), Art.Heart);
            livesText = UIFactory.Text(UIFactory.Box("Lives", bar, new Vector2(0, 0.5f), new Vector2(402, 0), new Vector2(140, 80)),
                "", 56, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
            waveText = UIFactory.Text(UIFactory.Box("Wave", bar, new Vector2(1, 0.5f), new Vector2(-270, 0), new Vector2(260, 80)),
                "", 44, TextAnchor.MiddleRight, UIFactory.TextDim, true);

            var speedButton = UIFactory.Button(UIFactory.Box("SpeedButton", bar, new Vector2(1, 0.5f), new Vector2(-142, 0), new Vector2(110, 96)),
                UIFactory.CardColor, out speedLabel, "x1", 40);
            speedButton.onClick.AddListener(ToggleSpeed);
            var pauseButton = UIFactory.Button(UIFactory.Box("PauseButton", bar, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(110, 96)),
                UIFactory.CardColor, out _, "II", 40);
            pauseButton.onClick.AddListener(() => SetPaused(true));
        }

        void BuildDebug(RectTransform root)
        {
            var area = UIFactory.Rect("Debug", root, new Vector2(0, 1), Vector2.one, new Vector2(Margin + 8, -Margin - 300), new Vector2(-Margin, -Margin - 140));
            debugRoot = area.gameObject;
            debugText = UIFactory.Text(UIFactory.Rect("DebugText", area, Vector2.zero, new Vector2(0.6f, 1), Vector2.zero, Vector2.zero),
                "", 26, TextAnchor.UpperLeft, new Color(0.75f, 1f, 0.75f, 0.9f));
            var stress = UIFactory.Button(UIFactory.Box("StressButton", area, new Vector2(1, 1), Vector2.zero, new Vector2(230, 70)),
                new Color(0f, 0f, 0f, 0.45f), out stressLabel, "", 26);
            stress.onClick.AddListener(() => { game.StressMode = !game.StressMode; Refresh(); });
        }

        void BuildBottomBar(RectTransform root)
        {
            var bar = UIFactory.Rect("BottomBar", root, Vector2.zero, new Vector2(1, 0), new Vector2(Margin, Margin), new Vector2(-Margin, Margin + 150));
            UIFactory.Image(bar, UIFactory.PanelColor, true);
            bottomBar = bar.gameObject;

            hintText = UIFactory.Text(UIFactory.Rect("Hint", bar, Vector2.zero, new Vector2(0.55f, 1), new Vector2(36, 0), Vector2.zero),
                "", 34, TextAnchor.MiddleLeft, UIFactory.TextDim);
            var rt = UIFactory.Rect("WaveButton", bar, new Vector2(0.55f, 0), Vector2.one, new Vector2(0, 18), new Vector2(-18, -18));
            waveButton = UIFactory.Button(rt, Art.Palette.Accent, out waveButtonLabel, "", 44);
            waveButtonImage = (Image)waveButton.targetGraphic;
            waveButton.onClick.AddListener(game.StartNextWave);
        }

        void BuildBuildPanel(RectTransform root)
        {
            var panel = BottomPanel("BuildPanel", root, out var title);
            buildPanel = panel.gameObject;
            title.text = "Build a tower";

            for (int i = 0; i < 3; i++)
            {
                var id = TowerIds[i];
                var type = game.Balance.Tower(id);
                float x0 = i / 3f, x1 = (i + 1) / 3f;
                var card = UIFactory.Rect("Build_" + id, panel, new Vector2(x0, 0), new Vector2(x1, 1),
                    new Vector2(i == 0 ? 24 : 12, 24), new Vector2(i == 2 ? -24 : -12, -96));
                buildButtons[i] = UIFactory.Button(card, UIFactory.CardColor, out var label, "", 32);
                label.text = "";
                buildButtons[i].onClick.AddListener(() => game.TryBuild(id));

                TowerIcon(card, id, new Vector2(0, -16), 120);
                UIFactory.Text(UIFactory.Box("Name", card, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(300, 50)),
                    type.label, 34, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
                UIFactory.Text(UIFactory.Box("Role", card, new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(300, 40)),
                    TowerRoles[i], 26, TextAnchor.MiddleCenter, UIFactory.TextDim);
                UIFactory.Icon(UIFactory.Box("CoinIcon", card, new Vector2(0.5f, 0), new Vector2(-50, 16), new Vector2(40, 40)), Art.Coin);
                buildCosts[i] = UIFactory.Text(UIFactory.Box("Cost", card, new Vector2(0.5f, 0), new Vector2(20, 10), new Vector2(120, 52)),
                    type.cost.ToString(), 36, TextAnchor.MiddleCenter, Art.Palette.Gold, true);
            }
        }

        void BuildTowerPanel(RectTransform root)
        {
            var panel = BottomPanel("TowerPanel", root, out towerTitle);
            towerPanel = panel.gameObject;

            var iconBox = UIFactory.Box("Icon", panel, new Vector2(0, 1), new Vector2(40, -110), new Vector2(170, 170));
            UIFactory.Icon(UIFactory.Fill("Base", iconBox), Art.TowerBase);
            towerIconTurret = UIFactory.Icon(UIFactory.Fill("Turret", iconBox), Art.MgTurret, 90f);
            towerStats = UIFactory.Text(UIFactory.Rect("Stats", panel, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(230, 24), new Vector2(0, -96)),
                "", 30, TextAnchor.MiddleLeft, UIFactory.TextMain);

            var upgrade = UIFactory.Button(UIFactory.Rect("UpgradeButton", panel, new Vector2(0.52f, 0.5f), new Vector2(1, 1), new Vector2(0, 8), new Vector2(-24, -96)),
                Art.Palette.Accent, out upgradeLabel, "", 36);
            upgradeImage = (Image)upgrade.targetGraphic;
            upgrade.onClick.AddListener(() => game.TryUpgrade());
            var sell = UIFactory.Button(UIFactory.Rect("SellButton", panel, new Vector2(0.52f, 0), new Vector2(1, 0.5f), new Vector2(0, 24), new Vector2(-24, -8)),
                new Color(0.45f, 0.16f, 0.18f), out sellLabel, "", 32);
            sell.onClick.AddListener(() => game.TrySell());
        }

        RectTransform BottomPanel(string name, RectTransform root, out Text title)
        {
            var panel = UIFactory.Rect(name, root, Vector2.zero, new Vector2(1, 0), new Vector2(Margin, Margin), new Vector2(-Margin, Margin + 400));
            UIFactory.Image(panel, UIFactory.PanelColor, true);
            title = UIFactory.Text(UIFactory.Rect("Title", panel, new Vector2(0, 1), Vector2.one, new Vector2(36, -90), new Vector2(-130, -10)),
                "", 40, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
            var close = UIFactory.Button(UIFactory.Box("Close", panel, Vector2.one, new Vector2(-20, -16), new Vector2(84, 70)),
                UIFactory.CardColor, out _, "X", 34);
            close.onClick.AddListener(game.Deselect);
            return panel;
        }

        void TowerIcon(RectTransform parent, string id, Vector2 topOffset, float size)
        {
            var box = UIFactory.Box("Icon", parent, new Vector2(0.5f, 1), topOffset, new Vector2(size, size));
            UIFactory.Icon(UIFactory.Fill("Base", box), Art.TowerBase);
            UIFactory.Icon(UIFactory.Fill("Turret", box), TurretSprite(id), 90f);
        }

        static Sprite TurretSprite(string id) => id == "cannon" ? Art.CannonTurret : id == "aa" ? Art.AaTurret : Art.MgTurret;

        void BuildPausePanel(Transform canvas)
        {
            var card = Overlay("PausePanel", canvas, out pausePanel, 640);
            UIFactory.Text(UIFactory.Box("Title", card, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(700, 100)),
                "Paused", 72, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            CardButton(card, "ResumeButton", "Resume", Art.Palette.Accent, -200, () => SetPaused(false));
            CardButton(card, "PauseRestartButton", "Restart level", UIFactory.CardColor, -340, () => { SetPaused(false); game.RestartLevel(); });
            CardButton(card, "PauseMenuButton", "Main menu", UIFactory.CardColor, -480, () => { SetPaused(false); game.ShowMenu(); });
        }

        void BuildResultPanel(Transform canvas)
        {
            var card = Overlay("ResultPanel", canvas, out resultPanel, 820);
            resultTitle = UIFactory.Text(UIFactory.Box("Title", card, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(760, 110)),
                "", 76, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            resultStats = UIFactory.Text(UIFactory.Box("Stats", card, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(760, 180)),
                "", 40, TextAnchor.MiddleCenter, UIFactory.TextDim);
            nextLevelButton = CardButton(card, "NextLevelButton", "Next level", Art.Palette.Accent, -400, game.NextLevel);
            CardButton(card, "RetryButton", "Retry", UIFactory.CardColor, -540, game.RestartLevel);
            CardButton(card, "MenuButton", "Main menu", UIFactory.CardColor, -680, game.ShowMenu);
        }

        RectTransform Overlay(string name, Transform canvas, out GameObject root, float cardHeight)
        {
            var dim = UIFactory.Fill(name, canvas);
            UIFactory.Image(dim, new Color(0f, 0f, 0f, 0.7f), true, false);
            root = dim.gameObject;
            var card = UIFactory.Box("Card", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, cardHeight));
            card.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(card, new Color(0.09f, 0.11f, 0.15f, 0.98f));
            return card;
        }

        static Button CardButton(RectTransform card, string name, string text, Color color, float y, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.Button(UIFactory.Box(name, card, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(620, 116)),
                color, out _, text, 44);
            b.onClick.AddListener(onClick);
            return b;
        }

        void BuildMenu(Transform canvas)
        {
            var root = UIFactory.Fill("Menu", canvas);
            UIFactory.Image(root, new Color(0.04f, 0.06f, 0.05f, 0.55f), true, false);
            menu = root.gameObject;

            UIFactory.Icon(UIFactory.Box("Logo", root, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(240, 240)), Art.Headquarters);
            UIFactory.Text(UIFactory.Box("Title", root, new Vector2(0.5f, 1), new Vector2(0, -540), new Vector2(1000, 150)),
                "COREHOLD", 120, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            UIFactory.Text(UIFactory.Box("Subtitle", root, new Vector2(0.5f, 1), new Vector2(0, -660), new Vector2(1000, 70)),
                "Hold the road. Protect the base.", 40, TextAnchor.MiddleCenter, UIFactory.TextDim);

            var levels = game.Balance.levels;
            for (int i = 0; i < levels.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(UIFactory.Box("Level_" + (i + 1), root, new Vector2(0.5f, 1), new Vector2(0, -820 - i * 170), new Vector2(760, 140)),
                    UIFactory.CardColor, out var label, "", 40);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(48, 0);
                label.text = $"<size=56>{i + 1}</size>    {levels[i].name}   <color=#9AA5B1><size=32>{levels[i].waves.Length} waves</size></color>";
                b.onClick.AddListener(() => game.LoadLevel(index));
            }
        }

        // ---------------- State ----------------

        void OnStateChanged()
        {
            var s = game.State;
            menu.SetActive(s == GameState.Menu);
            hud.SetActive(s != GameState.Menu);
            debugRoot.SetActive(s != GameState.Menu);
            resultPanel.SetActive(s == GameState.Won || s == GameState.Lost);
            pausePanel.SetActive(false);
            paused = false;
            if (s != GameState.Playing) speed = 1f;

            if (s == GameState.Won || s == GameState.Lost)
            {
                bool won = s == GameState.Won;
                bool last = game.LevelIndex == game.Balance.levels.Length - 1;
                resultTitle.text = won ? (last ? "All levels cleared!" : "Level complete") : "Level failed";
                resultTitle.color = won ? Art.Palette.Good : Art.Palette.Danger;
                resultStats.text = $"{game.Level.name}\nWaves {game.WavesStarted}/{game.WaveCount}   Kills {game.Kills}\nLives left {game.Lives}";
                nextLevelButton.gameObject.SetActive(won);
            }
            Refresh();
        }

        void ToggleSpeed()
        {
            speed = speed > 1f ? 1f : 2f;
            if (!paused) Time.timeScale = speed;
            Refresh();
        }

        void SetPaused(bool on)
        {
            paused = on;
            pausePanel.SetActive(on);
            Time.timeScale = on ? 0f : speed;
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
            bool playing = game.State == GameState.Playing;
            var sel = game.Selected;
            buildPanel.SetActive(playing && sel != null && sel.tower == null);
            towerPanel.SetActive(playing && sel != null && sel.tower != null);
            bottomBar.SetActive(playing && sel == null);
            if (game.Level == null) return;

            coinsText.text = game.Coins.ToString();
            livesText.text = game.Lives.ToString();
            waveText.text = $"Wave {game.WavesStarted}/{game.WaveCount}";
            speedLabel.text = speed > 1f ? "x2" : "x1";

            RefreshWaveButton();
            if (buildPanel.activeSelf) RefreshBuildPanel();
            if (towerPanel.activeSelf) RefreshTowerPanel(sel.tower);

            stressLabel.text = game.StressMode ? "Stress: ON" : "Stress: off";
            debugText.text =
                $"FPS {fps:0}  worst {worstMs:0.0} ms\n" +
                $"Enemies {game.EnemyCount}  Shots {game.ProjectileCount}\n" +
                $"Wave {game.WavesStarted}/{game.WaveCount}" +
                (game.PoolGrowth > 0 ? $"\nPool grew +{game.PoolGrowth}" : "");
        }

        void RefreshWaveButton()
        {
            bool all = game.WavesStarted >= game.WaveCount;
            waveButton.gameObject.SetActive(!all);
            if (game.WavesStarted == 0)
            {
                waveButtonLabel.text = "START";
                hintText.text = "Tap a pad to build,\nthen start the first wave";
            }
            else if (game.Spawning)
            {
                waveButtonLabel.text = "Incoming...";
                hintText.text = "Tap a tower to upgrade";
            }
            else if (!all)
            {
                waveButtonLabel.text = $"Next wave  {Mathf.CeilToInt(game.NextWaveTimer)}";
                hintText.text = "Call it early or get ready";
            }
            else hintText.text = "Last wave! Hold the base";
            waveButtonImage.color = game.CanStartNextWave ? Art.Palette.Accent : UIFactory.DisabledColor;
        }

        void RefreshBuildPanel()
        {
            for (int i = 0; i < 3; i++)
            {
                bool can = game.CanBuild(TowerIds[i]);
                ((Image)buildButtons[i].targetGraphic).color = can ? UIFactory.CardColor : UIFactory.DisabledColor;
                buildCosts[i].color = can ? Art.Palette.Gold : Art.Palette.Danger;
            }
        }

        void RefreshTowerPanel(Tower t)
        {
            var type = t.type;
            towerIconTurret.sprite = TurretSprite(type.id);
            towerTitle.text = $"{type.label}   <color=#F2C14E>Lv {t.level + 1}/{type.maxLevel + 1}</color>";

            bool maxed = t.level >= type.maxLevel;
            string Stat(string name, float now, float next, string fmt) =>
                maxed ? $"{name}  {now.ToString(fmt)}" : $"{name}  {now.ToString(fmt)} <color=#3DDC97>> {next.ToString(fmt)}</color>";
            towerStats.text =
                Stat("Damage", Formulas.TowerDamage(type, t.level), Formulas.TowerDamage(type, t.level + 1), "0") + "\n" +
                Stat("Rate", Formulas.TowerFireRate(type, t.level), Formulas.TowerFireRate(type, t.level + 1), "0.0#") + "/s\n" +
                Stat("Range", Formulas.TowerRange(type, t.level), Formulas.TowerRange(type, t.level + 1), "0.0#");

            if (maxed)
            {
                upgradeLabel.text = "MAX LEVEL";
                upgradeImage.color = UIFactory.DisabledColor;
            }
            else
            {
                upgradeLabel.text = $"Upgrade   <color=#F2C14E>{Formulas.UpgradeCost(type, t.level)}</color>";
                upgradeImage.color = game.CanUpgrade ? Art.Palette.Accent : UIFactory.DisabledColor;
            }
            sellLabel.text = $"Sell   <color=#F2C14E>+{Formulas.SellValue(t.spent, game.Balance.economy)}</color>";
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
