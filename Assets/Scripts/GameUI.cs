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
        const float HudHeight = 104f;
        const float SideWidth = 620f;
        static readonly string[] TowerIds = { "mg", "cannon", "aa" };
        static readonly string[] TowerRoles = { "Ground + air", "Ground, splash", "Air only" };
        static readonly Color[] TowerColors = { Art.Hex("4EA8FF"), Art.Hex("FF8A3D"), Art.Hex("FF5C7A") };
        static readonly Color ChipColor = new Color(0.07f, 0.1f, 0.16f, 0.82f);
        static readonly Color BubbleColor = new Color(0.16f, 0.21f, 0.31f, 1f);

        Game game;
        GameObject menu, hud, waveBar, buildPanel, towerPanel, resultPanel, pausePanel, debugRoot;
        Text coinsText, livesText, waveText, hintText, waveButtonLabel, speedLabel, debugText, stressLabel;
        Text towerTitle, upgradeLabel, sellLabel, resultTitle, resultStats;
        Image waveButtonImage, waveTimerFill, upgradeImage, towerIconTurret, towerHeader, resultHeader;
        Button waveButton, nextLevelButton;
        readonly Button[] buildButtons = new Button[3];
        readonly Text[] buildCosts = new Text[3];
        readonly Image[] towerPips = new Image[4];
        readonly Image[] resultStars = new Image[3];
        readonly StatBar[] statBars = new StatBar[3];
        float refreshTimer, speed = 1f;
        float maxDamage, maxRate, maxRange;
        bool paused;

        int fpsFrames;
        float fpsTime, fps, worstMs, worstMsWindow;

        // Horizontal bar: current value, plus the next level's value as a lighter ghost.
        sealed class StatBar
        {
            public Image now, next;
            public Text value;
        }

        public static GameUI Create(Game game)
        {
            UIFactory.EnsureEventSystem();
            // Soft dark edges over the 3D view, under all UI.
            var back = UIFactory.CreateCanvas("Vignette", 0);
            var vignette = UIFactory.Fill("Vignette", back.transform).gameObject.AddComponent<Image>();
            vignette.sprite = Art.Vignette;
            vignette.raycastTarget = false;
            Object.Destroy(back.GetComponent<GraphicRaycaster>());
            var canvas = UIFactory.CreateCanvas("UI", 10);
            var ui = canvas.gameObject.AddComponent<GameUI>();
            ui.Build(game, canvas.transform);
            return ui;
        }

        void Build(Game g, Transform canvas)
        {
            game = g;
            foreach (var t in game.Balance.towers)
            {
                maxDamage = Mathf.Max(maxDamage, Formulas.TowerDamage(t, t.maxLevel));
                maxRate = Mathf.Max(maxRate, Formulas.TowerFireRate(t, t.maxLevel));
                maxRange = Mathf.Max(maxRange, Formulas.TowerRange(t, t.maxLevel));
            }

            var safe = UIFactory.Fill("SafeArea", canvas);
            safe.gameObject.AddComponent<SafeArea>();

            BuildHud(safe);
            BuildDebug(safe);
            BuildWaveBar(safe);
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
            var bar = UIFactory.Rect("Hud", root, new Vector2(0, 1), Vector2.one, new Vector2(0, -Margin - HudHeight), new Vector2(0, -Margin));
            hud = bar.gameObject;

            coinsText = Chip(bar, "CoinChip", Margin + 30, 250, Art.Coin, Art.Palette.Gold);
            livesText = Chip(bar, "LivesChip", Margin + 310, 190, Art.Heart, UIFactory.TextMain);
            waveText = Chip(bar, "WaveChip", Margin + 530, 220, Art.Flag, UIFactory.TextMain);

            var pause = UIFactory.Box("PauseButton", bar, new Vector2(1, 1), new Vector2(-Margin, 0), new Vector2(104, 104));
            UIFactory.Shadow(pause, 12f, 6f);
            UIFactory.IconButton(pause, UIFactory.CardColor, Art.Pause, 44).onClick.AddListener(() => SetPaused(true));

            var speedRt = UIFactory.Box("SpeedButton", bar, new Vector2(1, 1), new Vector2(-Margin - 120, 0), new Vector2(104, 104));
            UIFactory.Shadow(speedRt, 12f, 6f);
            var speedButton = UIFactory.Button(speedRt, UIFactory.CardColor, out speedLabel, "x1", 38);
            speedButton.onClick.AddListener(ToggleSpeed);
        }

        // Shadow + background inside a panel container, so both hide together with it.
        static void PanelFace(RectTransform panel)
        {
            var face = UIFactory.Fill("Face", panel);
            UIFactory.Shadow(face);
            UIFactory.Image(face, UIFactory.PanelColor, true);    // also blocks taps from reaching the map
        }

        // Dark pill with a round icon bubble overlapping its left end, and a value.
        static Text Chip(RectTransform parent, string name, float x, float width, Sprite icon, Color textColor)
        {
            var chip = UIFactory.Box(name, parent, new Vector2(0, 1), new Vector2(x, -10), new Vector2(width, 84));
            UIFactory.Shadow(chip, 12f, 6f);
            UIFactory.Image(chip, ChipColor);
            var bubble = UIFactory.Box("Bubble", chip, new Vector2(0, 0.5f), new Vector2(-30, 0), new Vector2(100, 100));
            UIFactory.Icon(bubble, Art.Disc).color = BubbleColor;
            var ring = UIFactory.Icon(UIFactory.Fill("Ring", bubble), Art.Ring);
            ring.color = new Color(1f, 1f, 1f, 0.35f);
            UIFactory.Icon(UIFactory.Box("Icon", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58)), icon).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return UIFactory.Text(UIFactory.Rect("Value", chip, Vector2.zero, Vector2.one, new Vector2(82, 0), new Vector2(-14, 0)),
                "", 46, TextAnchor.MiddleLeft, textColor, true);
        }

        void BuildDebug(RectTransform root)
        {
            var area = UIFactory.Rect("Debug", root, new Vector2(0, 1), Vector2.one, new Vector2(Margin + 8, -Margin - 260), new Vector2(-Margin, -Margin - 130));
            debugRoot = area.gameObject;
            debugText = UIFactory.Text(UIFactory.Rect("DebugText", area, Vector2.zero, new Vector2(0.6f, 1), Vector2.zero, Vector2.zero),
                "", 24, TextAnchor.UpperLeft, new Color(0.85f, 1f, 0.85f, 0.85f));
            debugText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
            var stress = UIFactory.Button(UIFactory.Box("StressButton", area, new Vector2(1, 1), Vector2.zero, new Vector2(210, 64)),
                new Color(0f, 0f, 0f, 0.45f), out stressLabel, "", 24);
            stress.onClick.AddListener(() => { game.StressMode = !game.StressMode; Refresh(); });
        }

        // Wave button with a countdown bar, and a hint, in the top row right of the resource chips.
        void BuildWaveBar(RectTransform root)
        {
            var bar = UIFactory.Rect("WaveBar", root, new Vector2(0, 1), Vector2.one,
                new Vector2(Margin + 780, -Margin - HudHeight), new Vector2(-Margin - 250, -Margin));
            waveBar = bar.gameObject;

            var rt = UIFactory.Box("WaveButton", bar, new Vector2(0, 1), Vector2.zero, new Vector2(400, HudHeight));
            UIFactory.Shadow(rt, 12f, 6f);
            waveButton = UIFactory.Button(rt, UIFactory.Go, out waveButtonLabel, "", 38);
            waveButtonImage = (Image)waveButton.targetGraphic;
            waveButton.onClick.AddListener(game.StartNextWave);
            var track = UIFactory.Rect("Timer", rt, Vector2.zero, new Vector2(1, 0), new Vector2(26, 16), new Vector2(-26, 26));
            UIFactory.Image(track, new Color(0f, 0f, 0f, 0.3f)).sprite = Art.Pill;
            waveTimerFill = UIFactory.Image(UIFactory.Fill("Fill", track), new Color(1f, 1f, 1f, 0.85f));
            waveTimerFill.sprite = Art.Pixel;
            waveTimerFill.type = Image.Type.Filled;
            waveTimerFill.fillMethod = Image.FillMethod.Horizontal;
            waveButtonLabel.rectTransform.offsetMin = new Vector2(0f, 22f);

            hintText = UIFactory.Text(UIFactory.Rect("Hint", bar, Vector2.zero, Vector2.one, new Vector2(428, 0), Vector2.zero),
                "", 28, TextAnchor.MiddleLeft, Color.white);
            hintText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        }

        void BuildBuildPanel(RectTransform root)
        {
            var panel = SidePanel("BuildPanel", root, out var title, out _);
            buildPanel = panel.gameObject;
            title.text = "Build a tower";

            var area = UIFactory.Rect("Cards", panel, Vector2.zero, Vector2.one, new Vector2(0, 12), new Vector2(0, -100));
            for (int i = 0; i < 3; i++)
            {
                var id = TowerIds[i];
                var type = game.Balance.Tower(id);
                float y0 = 1f - (i + 1) / 3f, y1 = 1f - i / 3f;
                var card = UIFactory.Rect("Build_" + id, area, new Vector2(0, y0), new Vector2(1, y1), new Vector2(24, 10), new Vector2(-24, -10));
                buildButtons[i] = UIFactory.Button(card, UIFactory.CardColor, out var label, "", 32);
                label.text = "";
                buildButtons[i].onClick.AddListener(() => game.TryBuild(id));

                var strip = UIFactory.Rect("Accent", card, Vector2.zero, new Vector2(0, 1), new Vector2(10, 22), new Vector2(20, -14));
                UIFactory.Image(strip, TowerColors[i]).sprite = Art.Pill;
                var bubble = UIFactory.Box("Bubble", card, new Vector2(0, 0.5f), new Vector2(36, 4), new Vector2(150, 150));
                UIFactory.Icon(bubble, Art.Disc).color = new Color(0f, 0f, 0f, 0.25f);
                TowerIcon(bubble, id, 130f);
                UIFactory.Text(UIFactory.Box("Name", card, new Vector2(0, 0.5f), new Vector2(204, 30), new Vector2(260, 50)),
                    type.label, 36, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
                UIFactory.Text(UIFactory.Box("Role", card, new Vector2(0, 0.5f), new Vector2(204, -20), new Vector2(260, 40)),
                    TowerRoles[i], 26, TextAnchor.MiddleLeft, TowerColors[i]);

                var price = UIFactory.Box("Price", card, new Vector2(1, 0.5f), new Vector2(-22, 4), new Vector2(150, 64));
                UIFactory.Image(price, new Color(0f, 0f, 0f, 0.35f)).sprite = Art.Pill;
                UIFactory.Icon(UIFactory.Box("CoinIcon", price, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(44, 44)), Art.Coin);
                buildCosts[i] = UIFactory.Text(UIFactory.Rect("Cost", price, Vector2.zero, Vector2.one, new Vector2(58, 0), new Vector2(-14, 0)),
                    type.cost.ToString(), 36, TextAnchor.MiddleCenter, Art.Palette.Gold, true);
            }
        }

        void BuildTowerPanel(RectTransform root)
        {
            var panel = SidePanel("TowerPanel", root, out towerTitle, out towerHeader);
            towerPanel = panel.gameObject;

            var bubble = UIFactory.Box("Bubble", panel, new Vector2(0, 1), new Vector2(30, -112), new Vector2(170, 170));
            UIFactory.Icon(bubble, Art.Disc).color = new Color(0f, 0f, 0f, 0.25f);
            UIFactory.Icon(UIFactory.Box("Base", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140)), Art.TowerBase).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var turret = UIFactory.Box("Turret", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            turret.pivot = new Vector2(0.5f, 0.5f);
            towerIconTurret = UIFactory.Icon(turret, Art.MgTurret, 90f);

            // Level pips under the title.
            for (int i = 0; i < towerPips.Length; i++)
            {
                var pip = UIFactory.Box("Pip" + i, panel, new Vector2(0, 1), new Vector2(230 + i * 60, -120), new Vector2(48, 48));
                towerPips[i] = UIFactory.Icon(pip, Art.Star);
            }
            string[] names = { "Damage", "Rate", "Range" };
            for (int i = 0; i < 3; i++)
                statBars[i] = StatRow(panel, names[i], -196 - i * 64);

            var upgrade = UIFactory.Button(UIFactory.Rect("UpgradeButton", panel, Vector2.zero, new Vector2(1, 0), new Vector2(24, 172), new Vector2(-24, 310)),
                UIFactory.Go, out upgradeLabel, "", 38);
            upgradeImage = (Image)upgrade.targetGraphic;
            upgrade.onClick.AddListener(() => game.TryUpgrade());
            var sell = UIFactory.Button(UIFactory.Rect("SellButton", panel, Vector2.zero, new Vector2(1, 0), new Vector2(24, 24), new Vector2(-24, 146)),
                UIFactory.Bad, out sellLabel, "", 32);
            sell.onClick.AddListener(() => game.TrySell());
        }

        StatBar StatRow(RectTransform panel, string name, float y)
        {
            var row = UIFactory.Rect(name, panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(230, y - 50), new Vector2(-24, y));
            UIFactory.Text(UIFactory.Rect("Label", row, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(120, 0)),
                name, 26, TextAnchor.MiddleLeft, UIFactory.TextDim);
            var track = UIFactory.Rect("Track", row, Vector2.zero, Vector2.one, new Vector2(124, 14), new Vector2(-96, -14));
            UIFactory.Image(track, new Color(0f, 0f, 0f, 0.35f)).sprite = Art.Pill;
            var bar = new StatBar
            {
                next = FillBar(track, "Next", new Color(0.24f, 0.86f, 0.59f, 0.6f)),
                now = FillBar(track, "Now", Art.Palette.Gold),
                value = UIFactory.Text(UIFactory.Rect("Value", row, new Vector2(1, 0), Vector2.one, new Vector2(-90, 0), Vector2.zero),
                    "", 26, TextAnchor.MiddleRight, UIFactory.TextMain),
            };
            return bar;
        }

        static Image FillBar(RectTransform track, string name, Color color)
        {
            var img = UIFactory.Image(UIFactory.Rect(name, track, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3)), color);
            img.sprite = Art.Pixel;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            return img;
        }

        // Full-height panel on one side of the screen, below the HUD, with a coloured title strip.
        RectTransform SidePanel(string name, RectTransform root, out Text title, out Image header)
        {
            var panel = UIFactory.Rect(name, root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            PlaceSidePanel(panel, false);
            PanelFace(panel);
            var strip = UIFactory.Rect("Header", panel, new Vector2(0, 1), Vector2.one, new Vector2(0, -92), Vector2.zero);
            header = UIFactory.Image(strip, new Color(0.2f, 0.27f, 0.4f, 1f));
            header.sprite = Art.Header;
            title = UIFactory.Text(UIFactory.Rect("Title", panel, new Vector2(0, 1), Vector2.one, new Vector2(30, -92), new Vector2(-120, 0)),
                "", 40, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
            var close = UIFactory.IconButton(UIFactory.Box("Close", panel, Vector2.one, new Vector2(-14, -10), new Vector2(78, 72)),
                UIFactory.Bad, Art.Close, 30);
            close.onClick.AddListener(game.Deselect);
            return panel;
        }

        // Panels open on the side away from the selected pad so it stays visible.
        static void PlaceSidePanel(RectTransform panel, bool left)
        {
            float x = left ? 0f : 1f;
            panel.anchorMin = new Vector2(x, 0f);
            panel.anchorMax = new Vector2(x, 1f);
            panel.offsetMin = new Vector2(left ? Margin : -Margin - SideWidth, Margin);
            panel.offsetMax = new Vector2(left ? Margin + SideWidth : -Margin, -Margin - HudHeight - 20f);
        }

        static void TowerIcon(RectTransform parent, string id, float size)
        {
            var b = UIFactory.Box("Base", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            b.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Icon(b, Art.TowerBase);
            var t = UIFactory.Box("Turret", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            t.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Icon(t, TurretSprite(id), 90f);
        }

        static Sprite TurretSprite(string id) => id == "cannon" ? Art.CannonTurret : id == "aa" ? Art.AaTurret : Art.MgTurret;

        void BuildPausePanel(Transform canvas)
        {
            var card = Overlay("PausePanel", canvas, out pausePanel, 600, out var header);
            header.color = UIFactory.Primary;
            UIFactory.Text(UIFactory.Rect("Title", card, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero),
                "Paused", 60, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            CardButton(card, "ResumeButton", "Resume", UIFactory.Go, -170, () => SetPaused(false));
            CardButton(card, "PauseRestartButton", "Restart level", UIFactory.CardColor, -310, () => { SetPaused(false); game.RestartLevel(); });
            CardButton(card, "PauseMenuButton", "Main menu", UIFactory.CardColor, -450, () => { SetPaused(false); game.ShowMenu(); });
        }

        void BuildResultPanel(Transform canvas)
        {
            var card = Overlay("ResultPanel", canvas, out resultPanel, 860, out resultHeader);
            resultTitle = UIFactory.Text(UIFactory.Rect("Title", card, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero),
                "", 60, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 130f : 100f;
                var star = UIFactory.Box("Star" + i, card, new Vector2(0.5f, 1), new Vector2((i - 1) * 140f, i == 1 ? -140f : -160f), new Vector2(size, size));
                star.pivot = new Vector2(0.5f, 1f);
                resultStars[i] = UIFactory.Icon(star, Art.Star);
            }
            resultStats = UIFactory.Text(UIFactory.Box("Stats", card, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(760, 150)),
                "", 36, TextAnchor.MiddleCenter, UIFactory.TextDim);
            resultStats.rectTransform.pivot = new Vector2(0.5f, 1f);
            nextLevelButton = CardButton(card, "NextLevelButton", "Next level", UIFactory.Go, -460, game.NextLevel);
            CardButton(card, "RetryButton", "Retry", UIFactory.Warn, -600, game.RestartLevel);
            CardButton(card, "MenuButton", "Main menu", UIFactory.CardColor, -740, game.ShowMenu);
        }

        RectTransform Overlay(string name, Transform canvas, out GameObject root, float cardHeight, out Image header)
        {
            var dim = UIFactory.Fill(name, canvas);
            UIFactory.Image(dim, new Color(0.02f, 0.03f, 0.06f, 0.6f), true, false);
            root = dim.gameObject;
            var card = UIFactory.Box("Card", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, cardHeight));
            card.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Shadow(card, 30f, 16f);
            UIFactory.Image(card, new Color(0.12f, 0.16f, 0.24f, 0.98f));
            header = UIFactory.Image(UIFactory.Rect("Header", card, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero), UIFactory.Primary);
            header.sprite = Art.Header;
            return card;
        }

        static Button CardButton(RectTransform card, string name, string text, Color color, float y, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIFactory.Box(name, card, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 112));
            rt.pivot = new Vector2(0.5f, 1f);
            var b = UIFactory.Button(rt, color, out _, text, 42);
            b.onClick.AddListener(onClick);
            return b;
        }

        // Left column over the 3D garage: title, then one card per level.
        void BuildMenu(Transform canvas)
        {
            var root = UIFactory.Fill("Menu", canvas);
            menu = root.gameObject;
            var column = UIFactory.Rect("Column", root, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(860, 0));
            var fade = UIFactory.Image(column, new Color(0.03f, 0.05f, 0.09f, 0.72f), false, false);
            fade.sprite = Art.Fade;     // soft edge toward the garage

            var safe = UIFactory.Fill("SafeArea", column);
            UIFactory.Text(UIFactory.Box("Title", safe, new Vector2(0, 1), new Vector2(90, -70), new Vector2(760, 130)),
                "COREHOLD", 112, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
            UIFactory.Text(UIFactory.Box("Subtitle", safe, new Vector2(0, 1), new Vector2(96, -200), new Vector2(760, 50)),
                "Hold the road. Protect the base.", 34, TextAnchor.MiddleLeft, Art.Palette.Gold);

            var levels = game.Balance.levels;
            for (int i = 0; i < levels.Length; i++)
            {
                int index = i;
                var rt = UIFactory.Box("Level_" + (i + 1), safe, new Vector2(0, 1), new Vector2(90, -300 - i * 170), new Vector2(680, 140));
                UIFactory.Shadow(rt, 14f, 8f);
                var b = UIFactory.Button(rt, i == 0 ? UIFactory.Go : UIFactory.Primary, out var label, "", 44);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(150, 8);
                label.text = $"{levels[i].name}\n<size=28><color=#FFFFFFB0>{levels[i].waves.Length} waves  ·  {levels[i].lives} lives</color></size>";
                var badge = UIFactory.Box("Badge", rt, new Vector2(0, 0.5f), new Vector2(26, 4), new Vector2(100, 100));
                UIFactory.Icon(badge, Art.Disc).color = new Color(0f, 0f, 0f, 0.28f);
                UIFactory.Text(UIFactory.Fill("Number", badge), (i + 1).ToString(), 56, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
                UIFactory.Icon(UIFactory.Box("Play", rt, new Vector2(1, 0.5f), new Vector2(-36, 4), new Vector2(54, 54)), Art.Play);
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
                resultTitle.text = won ? (last ? "All levels cleared!" : "Victory!") : "Base destroyed";
                resultHeader.color = won ? UIFactory.Go : UIFactory.Bad;
                int stars = !won ? 0 : game.Lives >= game.Level.lives ? 3 : game.Lives * 2 >= game.Level.lives ? 2 : 1;
                for (int i = 0; i < 3; i++)
                    resultStars[i].color = i < stars ? Art.Palette.Gold : new Color(0f, 0f, 0f, 0.35f);
                resultStats.text = $"{game.Level.name}\nWaves {game.WavesStarted}/{game.WaveCount}    Kills {game.Kills}    Lives {game.Lives}";
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
            waveBar.SetActive(playing);
            if (sel != null)
            {
                // Side panels cover about a third of the screen: open them on the half without the pad.
                bool left = sel.position.x > 0f;
                PlaceSidePanel((RectTransform)buildPanel.transform, left);
                PlaceSidePanel((RectTransform)towerPanel.transform, left);
            }
            if (game.Level == null) return;

            coinsText.text = game.Coins.ToString();
            livesText.text = game.Lives.ToString();
            waveText.text = $"{game.WavesStarted}/{game.WaveCount}";
            speedLabel.text = speed > 1f ? "x2" : "x1";

            RefreshWaveButton();
            if (buildPanel.activeSelf) RefreshBuildPanel();
            if (towerPanel.activeSelf) RefreshTowerPanel(sel.tower);

            stressLabel.text = game.StressMode ? "Stress: ON" : "Stress: off";
            debugText.text =
                $"FPS {fps:0}  worst {worstMs:0.0} ms\n" +
                $"Enemies {game.EnemyCount}  Shots {game.ProjectileCount}" +
                (game.PoolGrowth > 0 ? $"\nPool grew +{game.PoolGrowth}" : "");
        }

        void RefreshWaveButton()
        {
            bool all = game.WavesStarted >= game.WaveCount;
            waveButton.gameObject.SetActive(!all);
            float delay = game.WavesStarted == 0 ? game.Balance.economy.firstWaveDelay : game.Balance.economy.timeBetweenWaves;
            bool counting = !all && !game.Spawning;
            waveTimerFill.transform.parent.gameObject.SetActive(counting);
            if (counting) waveTimerFill.fillAmount = Mathf.Clamp01(game.NextWaveTimer / Mathf.Max(delay, 0.01f));

            int seconds = Mathf.CeilToInt(game.NextWaveTimer);
            if (game.WavesStarted == 0)
            {
                waveButtonLabel.text = $"START  {seconds}";
                hintText.text = "Build towers on the pads.\nThe enemy attacks soon!";
            }
            else if (game.Spawning)
            {
                waveButtonLabel.text = "Incoming...";
                hintText.text = "Tap a tower to upgrade it";
            }
            else if (!all)
            {
                waveButtonLabel.text = $"Next wave  {seconds}";
                hintText.text = "Call it early for a head start";
            }
            else hintText.text = "Last wave! Hold the base";
            waveButtonImage.color = game.CanStartNextWave ? UIFactory.Go : UIFactory.DisabledColor;
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
            int index = System.Array.IndexOf(TowerIds, type.id);
            towerIconTurret.sprite = TurretSprite(type.id);
            towerTitle.text = type.label;
            towerHeader.color = index >= 0 ? MeshBuilder.Shade(TowerColors[index], 0.75f) : UIFactory.CardColor;
            for (int i = 0; i < towerPips.Length; i++)
            {
                towerPips[i].gameObject.SetActive(i <= type.maxLevel);
                towerPips[i].color = i <= t.level ? Art.Palette.Gold : new Color(0f, 0f, 0f, 0.35f);
            }

            bool maxed = t.level >= type.maxLevel;
            int next = maxed ? t.level : t.level + 1;
            SetStat(statBars[0], Formulas.TowerDamage(type, t.level), Formulas.TowerDamage(type, next), maxDamage, "0");
            SetStat(statBars[1], Formulas.TowerFireRate(type, t.level), Formulas.TowerFireRate(type, next), maxRate, "0.0");
            SetStat(statBars[2], Formulas.TowerRange(type, t.level), Formulas.TowerRange(type, next), maxRange, "0.0");

            if (maxed)
            {
                upgradeLabel.text = "MAX LEVEL";
                upgradeImage.color = UIFactory.DisabledColor;
            }
            else
            {
                upgradeLabel.text = $"Upgrade   <color=#FFE08A>{Formulas.UpgradeCost(type, t.level)}</color>";
                upgradeImage.color = game.CanUpgrade ? UIFactory.Go : UIFactory.DisabledColor;
            }
            sellLabel.text = $"Sell   <color=#FFE08A>+{Formulas.SellValue(t.spent, game.Balance.economy)}</color>";
        }

        static void SetStat(StatBar bar, float now, float next, float max, string fmt)
        {
            bar.now.fillAmount = now / max;
            bar.next.fillAmount = next / max;
            bar.value.text = next > now ? $"{now.ToString(fmt)}<color=#3DDC97>+{(next - now).ToString(fmt)}</color>" : now.ToString(fmt);
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
