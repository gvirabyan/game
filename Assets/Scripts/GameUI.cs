using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Corehold
{
    // Menu, HUD, build/upgrade panels, pause, result screen and debug overlay, all built from code.
    // Object names (ModeLevels, Level_1, WaveButton, Build_mg, UpgradeButton, ...) are used by the PlayMode tests.
    public sealed class GameUI : MonoBehaviour
    {
        const float RefreshInterval = 0.1f;
        const float Margin = 24f;
        const float HudHeight = 104f;
        const float SideWidth = 620f;
        const int PreviewSlots = 4;
        static readonly Color ChipColor = new Color(0.07f, 0.1f, 0.16f, 0.82f);
        static readonly Color BubbleColor = new Color(0.16f, 0.21f, 0.31f, 1f);
        static readonly Color Shade = new Color(0f, 0f, 0f, 0.3f);

        Game game;
        GameObject menuHome, menuLevels, menuRoot, hud, waveBar, buildPanel, towerPanel, resultPanel, pausePanel, debugRoot;
        Text coinsText, livesText, waveText, waveButtonLabel, speedLabel, debugText, stressLabel, previewLabel, endlessBestText;
        Text towerTitle, upgradeLabel, sellLabel, resultTitle, resultStats;
        Image waveButtonImage, waveTimerFill, upgradeImage, towerIcon, towerHeader, resultHeader;
        Button waveButton, nextLevelButton;
        readonly List<Button> buildButtons = new List<Button>();
        readonly List<Text> buildCosts = new List<Text>();
        readonly Image[] towerPips = new Image[4];
        readonly Image[] resultStars = new Image[3];
        readonly StatBar[] statBars = new StatBar[3];
        readonly Image[] previewIcons = new Image[PreviewSlots];
        readonly Text[] previewCounts = new Text[PreviewSlots];
        readonly List<Image[]> levelStars = new List<Image[]>();
        readonly List<Image> soundIcons = new List<Image>();
        readonly Dictionary<string, Color> towerColors = new Dictionary<string, Color>();
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
            string[] palette = { "4EA8FF", "FF8A3D", "FF5C7A", "FFB13D", "8FE3FF", "B08A5A", "C9D36A", "7FD8E8" };
            for (int i = 0; i < game.Balance.towers.Length; i++)
            {
                var t = game.Balance.towers[i];
                towerColors[t.id] = Art.Hex(palette[i % palette.Length]);
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

        // ---------------- HUD ----------------

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
            UIFactory.Button(speedRt, UIFactory.CardColor, out speedLabel, "x1", 38).onClick.AddListener(ToggleSpeed);

            SoundButton(bar, "SoundButton", new Vector2(-Margin - 240, 0));
        }

        // Toggles all audio; every sound button shows the same state.
        void SoundButton(RectTransform parent, string name, Vector2 position)
        {
            var rt = UIFactory.Box(name, parent, new Vector2(1, 1), position, new Vector2(104, 104));
            UIFactory.Shadow(rt, 12f, 6f);
            var b = UIFactory.IconButton(rt, UIFactory.CardColor, Art.SoundOn, 50);
            soundIcons.Add(rt.Find("Icon").GetComponent<Image>());
            b.onClick.AddListener(() =>
            {
                Sfx.SetMuted(!Sfx.Muted);
                foreach (var icon in soundIcons) icon.sprite = Sfx.Muted ? Art.SoundOff : Art.SoundOn;
            });
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
            UIFactory.Icon(UIFactory.Fill("Ring", bubble), Art.Ring).color = new Color(1f, 1f, 1f, 0.35f);
            Centered(UIFactory.Icon(UIFactory.Box("Icon", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58)), icon));
            return UIFactory.Text(UIFactory.Rect("Value", chip, Vector2.zero, Vector2.one, new Vector2(82, 0), new Vector2(-14, 0)),
                "", 46, TextAnchor.MiddleLeft, textColor, true);
        }

        static Image Centered(Image img)
        {
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
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

        // Wave button with a countdown bar, then a preview of the next wave's enemies.
        void BuildWaveBar(RectTransform root)
        {
            var bar = UIFactory.Rect("WaveBar", root, new Vector2(0, 1), Vector2.one,
                new Vector2(Margin + 780, -Margin - HudHeight), new Vector2(-Margin - 370, -Margin));
            waveBar = bar.gameObject;

            var rt = UIFactory.Box("WaveButton", bar, new Vector2(0, 1), Vector2.zero, new Vector2(380, HudHeight));
            UIFactory.Shadow(rt, 12f, 6f);
            waveButton = UIFactory.Button(rt, UIFactory.Go, out waveButtonLabel, "", 36);
            waveButtonImage = (Image)waveButton.targetGraphic;
            waveButton.onClick.AddListener(game.StartNextWave);
            var track = UIFactory.Rect("Timer", rt, Vector2.zero, new Vector2(1, 0), new Vector2(26, 16), new Vector2(-26, 26));
            UIFactory.Image(track, new Color(0f, 0f, 0f, 0.3f)).sprite = Art.Pill;
            waveTimerFill = UIFactory.Image(UIFactory.Fill("Fill", track), new Color(1f, 1f, 1f, 0.85f));
            waveTimerFill.sprite = Art.Pixel;
            waveTimerFill.type = Image.Type.Filled;
            waveTimerFill.fillMethod = Image.FillMethod.Horizontal;
            waveButtonLabel.rectTransform.offsetMin = new Vector2(0f, 22f);

            var preview = UIFactory.Box("Preview", bar, new Vector2(0, 1), new Vector2(400, -10), new Vector2(92 + PreviewSlots * 118, 84));
            UIFactory.Image(preview, ChipColor);
            previewLabel = UIFactory.Text(UIFactory.Rect("Label", preview, Vector2.zero, new Vector2(0, 1), new Vector2(14, 0), new Vector2(86, 0)),
                "NEXT", 22, TextAnchor.MiddleCenter, UIFactory.TextDim);
            for (int i = 0; i < PreviewSlots; i++)
            {
                var slot = UIFactory.Box("Slot" + i, preview, new Vector2(0, 0.5f), new Vector2(92 + i * 118, 0), new Vector2(112, 76));
                previewIcons[i] = UIFactory.Icon(UIFactory.Box("Icon", slot, new Vector2(0, 0.5f), Vector2.zero, new Vector2(70, 70)), Art.Coin);
                previewCounts[i] = UIFactory.Text(UIFactory.Rect("Count", slot, Vector2.zero, Vector2.one, new Vector2(58, 0), Vector2.zero),
                    "", 26, TextAnchor.LowerLeft, UIFactory.TextMain, true);
            }
        }

        // ---------------- Build and tower panels ----------------

        // Two columns of tower cards with a 3D icon, name, role and price.
        void BuildBuildPanel(RectTransform root)
        {
            var panel = SidePanel("BuildPanel", root, out var title, out _);
            buildPanel = panel.gameObject;
            title.text = "Build";

            var towers = game.Balance.towers;
            int rows = (towers.Length + 1) / 2;
            var area = UIFactory.Rect("Cards", panel, Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -100));
            for (int i = 0; i < towers.Length; i++)
            {
                var type = towers[i];
                var id = type.id;
                int col = i % 2, row = i / 2;
                var card = UIFactory.Rect("Build_" + id, area, new Vector2(col * 0.5f, 1f - (row + 1f) / rows), new Vector2((col + 1) * 0.5f, 1f - row / (float)rows),
                    new Vector2(8, 6), new Vector2(-8, -6));
                var button = UIFactory.Button(card, UIFactory.CardColor, out var label, "", 24);
                label.text = "";
                button.onClick.AddListener(() => game.TryBuild(id));
                buildButtons.Add(button);

                var strip = UIFactory.Rect("Accent", card, new Vector2(0, 1), Vector2.one, new Vector2(18, -12), new Vector2(-18, -6));
                UIFactory.Image(strip, towerColors[id]).sprite = Art.Pill;
                var icon = UIFactory.Box("Icon", card, new Vector2(0, 0.5f), new Vector2(4, 6), new Vector2(118, 118));
                UIFactory.Icon(icon, Icons.Tower(id, 0));
                UIFactory.Text(UIFactory.Rect("Name", card, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(122, 0), new Vector2(-8, -14)),
                    type.label, 25, TextAnchor.LowerLeft, UIFactory.TextMain, true);
                UIFactory.Text(UIFactory.Rect("Role", card, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(122, -30), new Vector2(-8, 0)),
                    type.role, 20, TextAnchor.UpperLeft, towerColors[id]);

                var price = UIFactory.Box("Price", card, new Vector2(1, 0), new Vector2(-10, 14), new Vector2(116, 44));
                UIFactory.Image(price, Shade).sprite = Art.Pill;
                UIFactory.Icon(UIFactory.Box("CoinIcon", price, new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(34, 34)), Art.Coin);
                buildCosts.Add(UIFactory.Text(UIFactory.Rect("Cost", price, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-8, 0)),
                    type.cost.ToString(), 30, TextAnchor.MiddleCenter, Art.Palette.Gold, true));
            }
        }

        void BuildTowerPanel(RectTransform root)
        {
            var panel = SidePanel("TowerPanel", root, out towerTitle, out towerHeader);
            towerPanel = panel.gameObject;

            var bubble = UIFactory.Box("Bubble", panel, new Vector2(0, 1), new Vector2(26, -108), new Vector2(180, 180));
            UIFactory.Icon(bubble, Art.Disc).color = Shade;
            towerIcon = Centered(UIFactory.Icon(UIFactory.Box("Icon", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176, 176)), Art.TowerBase));

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
            return new StatBar
            {
                next = FillBar(track, "Next", new Color(0.24f, 0.86f, 0.59f, 0.6f)),
                now = FillBar(track, "Now", Art.Palette.Gold),
                value = UIFactory.Text(UIFactory.Rect("Value", row, new Vector2(1, 0), Vector2.one, new Vector2(-90, 0), Vector2.zero),
                    "", 26, TextAnchor.MiddleRight, UIFactory.TextMain),
            };
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

        // ---------------- Pause and result ----------------

        void BuildPausePanel(Transform canvas)
        {
            var card = Overlay("PausePanel", canvas, out pausePanel, 600, out var header);
            header.color = UIFactory.Primary;
            UIFactory.Text(UIFactory.Rect("Title", card, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero),
                "Paused", 60, TextAnchor.MiddleCenter, UIFactory.TextMain, true);
            CardButton(card, "ResumeButton", "Resume", UIFactory.Go, -170, () => SetPaused(false));
            CardButton(card, "PauseRestartButton", "Restart", UIFactory.CardColor, -310, () => { SetPaused(false); game.RestartLevel(); });
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

        // ---------------- Main menu ----------------

        // Home: two game modes over the garage. Missions: level cards with a mini map of each road.
        void BuildMenu(Transform canvas)
        {
            var root = UIFactory.Fill("Menu", canvas);
            menuRoot = root.gameObject;

            var home = UIFactory.Fill("Home", root);
            menuHome = home.gameObject;
            var column = UIFactory.Rect("Column", home, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(900, 0));
            UIFactory.Image(column, new Color(0.03f, 0.05f, 0.09f, 0.75f), false, false).sprite = Art.Fade;
            var safe = UIFactory.Fill("SafeArea", column);
            UIFactory.Text(UIFactory.Box("Title", safe, new Vector2(0, 1), new Vector2(90, -80), new Vector2(760, 130)),
                "COREHOLD", 112, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
            UIFactory.Text(UIFactory.Box("Subtitle", safe, new Vector2(0, 1), new Vector2(96, -210), new Vector2(760, 50)),
                "Hold the road. Protect the base.", 34, TextAnchor.MiddleLeft, Art.Palette.Gold);

            ModeButton(safe, "ModeLevels", "MISSIONS", $"{game.Balance.levels.Length} levels, growing threats", UIFactory.Go, Art.Flag, -330,
                () => ShowLevelSelect(true));
            endlessBestText = ModeButton(safe, "ModeEndless", "ENDLESS", "", UIFactory.Warn, Art.Heart, -530, game.StartEndless);
            SoundButton(root, "MenuSoundButton", new Vector2(-Margin, -Margin));

            BuildLevelSelect(root);
        }

        Text ModeButton(RectTransform parent, string name, string title, string subtitle, Color color, Sprite icon, float y, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIFactory.Box(name, parent, new Vector2(0, 1), new Vector2(90, y), new Vector2(720, 170));
            UIFactory.Shadow(rt, 16f, 10f);
            var b = UIFactory.Button(rt, color, out var label, "", 56);
            label.alignment = TextAnchor.UpperLeft;
            label.rectTransform.offsetMin = new Vector2(190, 8);
            label.rectTransform.offsetMax = new Vector2(0, -26);
            label.text = title;
            var badge = UIFactory.Box("Badge", rt, new Vector2(0, 0.5f), new Vector2(30, 6), new Vector2(130, 130));
            UIFactory.Icon(badge, Art.Disc).color = new Color(0f, 0f, 0f, 0.25f);
            Centered(UIFactory.Icon(UIFactory.Box("Icon", badge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76)), icon));
            var sub = UIFactory.Text(UIFactory.Rect("Sub", rt, Vector2.zero, Vector2.one, new Vector2(192, 30), new Vector2(-90, -100)),
                subtitle, 28, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
            UIFactory.Icon(UIFactory.Box("Play", rt, new Vector2(1, 0.5f), new Vector2(-36, 6), new Vector2(60, 60)), Art.Play);
            b.onClick.AddListener(onClick);
            return sub;
        }

        void BuildLevelSelect(RectTransform root)
        {
            var screen = UIFactory.Fill("LevelSelect", root);
            menuLevels = screen.gameObject;
            UIFactory.Image(screen, new Color(0.03f, 0.05f, 0.09f, 0.82f), true, false);
            var safe = UIFactory.Fill("SafeArea", screen);
            safe.gameObject.AddComponent<SafeArea>();

            var back = UIFactory.Box("BackButton", safe, new Vector2(0, 1), new Vector2(Margin + 10, -Margin), new Vector2(200, 96));
            UIFactory.Shadow(back, 12f, 6f);
            UIFactory.Button(back, UIFactory.CardColor, out _, "Back", 36).onClick.AddListener(() => ShowLevelSelect(false));
            UIFactory.Text(UIFactory.Rect("Title", safe, new Vector2(0, 1), Vector2.one, new Vector2(0, -130), new Vector2(0, -Margin)),
                "Select mission", 60, TextAnchor.MiddleCenter, UIFactory.TextMain, true);

            var levels = game.Balance.levels;
            int cols = 3, rows = (levels.Length + cols - 1) / cols;
            var grid = UIFactory.Rect("Grid", safe, Vector2.zero, Vector2.one, new Vector2(40, 30), new Vector2(-40, -150));
            for (int i = 0; i < levels.Length; i++)
            {
                int index = i;
                var level = levels[i];
                int col = i % cols, row = i / cols;
                var card = UIFactory.Rect("Level_" + (i + 1), grid, new Vector2(col / (float)cols, 1f - (row + 1f) / rows),
                    new Vector2((col + 1f) / cols, 1f - row / (float)rows), new Vector2(14, 12), new Vector2(-14, -12));
                UIFactory.Shadow(card, 14f, 8f);
                var b = UIFactory.Button(card, UIFactory.CardColor, out var label, "", 24);
                label.text = "";
                b.onClick.AddListener(() => game.LoadLevel(index));

                var map = UIFactory.Box("Map", card, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(500, 250));
                map.pivot = new Vector2(0.5f, 1f);
                MiniMap(map, level.path, level.spots);

                var badge = UIFactory.Box("Badge", card, new Vector2(0, 1), new Vector2(10, -6), new Vector2(84, 84));
                UIFactory.Icon(badge, Art.Disc).color = UIFactory.Primary;
                UIFactory.Text(UIFactory.Fill("Number", badge), (i + 1).ToString(), 44, TextAnchor.MiddleCenter, UIFactory.TextMain, true);

                UIFactory.Text(UIFactory.Rect("Name", card, Vector2.zero, new Vector2(1, 0), new Vector2(26, 70), new Vector2(-180, 124)),
                    level.name, 40, TextAnchor.MiddleLeft, UIFactory.TextMain, true);
                UIFactory.Text(UIFactory.Rect("Info", card, Vector2.zero, new Vector2(1, 0), new Vector2(26, 24), new Vector2(-180, 70)),
                    $"{level.waves.Length} waves  Â·  {level.lives} lives", 26, TextAnchor.MiddleLeft, UIFactory.TextDim);

                var stars = new Image[3];
                for (int s = 0; s < 3; s++)
                    stars[s] = UIFactory.Icon(UIFactory.Box("Star" + s, card, new Vector2(1, 0), new Vector2(-24 - (2 - s) * 52, 76), new Vector2(48, 48)), Art.Star);
                levelStars.Add(stars);

                // Enemy types in this mission.
                var types = new List<string>();
                foreach (var w in level.waves)
                    foreach (var g in w.groups)
                        if (!types.Contains(g.enemy)) types.Add(g.enemy);
                for (int k = 0; k < types.Count && k < 7; k++)
                    UIFactory.Icon(UIFactory.Box("Enemy" + k, card, new Vector2(1, 0), new Vector2(-20 - k * 46, 20), new Vector2(50, 50)), Icons.Enemy(types[k]));
            }
        }

        // Grass field with the road, pads and base, scaled into the box.
        static void MiniMap(RectTransform box, Vector2[] road, Vector2[] pads)
        {
            var area = CameraRig.PlayArea;
            var size = box.sizeDelta;
            float scale = Mathf.Min(size.x / area.width, size.y / area.height);
            Vector2 P(Vector2 p) => new Vector2((p.x - area.center.x) * scale, (p.y - area.center.y) * scale);

            UIFactory.Image(box, Art.Hex("5E9A44"));
            for (int i = 0; i < road.Length - 1; i++)
            {
                var a = road[i];
                var b = road[i + 1];
                if (i == 0) a = Vector2.MoveTowards(b, a, Mathf.Min((a - b).magnitude, b.x - area.xMin));   // entrance stays inside the box
                var pa = P(a);
                var pb = P(b);
                foreach (var (width, color) in new[] { (0.95f * scale + 6f, Art.Hex("85663F")), (0.95f * scale, Art.Hex("D2AE76")) })
                {
                    var seg = UIFactory.Box("Road", box, new Vector2(0.5f, 0.5f), (pa + pb) * 0.5f, new Vector2((pb - pa).magnitude + width, width));
                    seg.pivot = new Vector2(0.5f, 0.5f);
                    seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
                    UIFactory.Image(seg, color, false, false);
                }
            }
            foreach (var pad in pads)
            {
                var dot = UIFactory.Box("Pad", box, new Vector2(0.5f, 0.5f), P(pad), new Vector2(0.8f * scale, 0.8f * scale));
                dot.pivot = new Vector2(0.5f, 0.5f);
                UIFactory.Icon(dot, Art.Disc).color = new Color(0.85f, 0.88f, 0.9f, 0.95f);
            }
            var hq = UIFactory.Box("Base", box, new Vector2(0.5f, 0.5f), P(road[road.Length - 1]), new Vector2(1.9f * scale, 1.9f * scale));
            hq.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Icon(hq, Art.Headquarters);
        }

        void ShowLevelSelect(bool on)
        {
            menuHome.SetActive(!on);
            menuLevels.SetActive(on);
            if (on)
                for (int i = 0; i < levelStars.Count; i++)
                    for (int s = 0; s < 3; s++)
                        levelStars[i][s].color = s < game.LevelStars[i] ? Art.Palette.Gold : new Color(0f, 0f, 0f, 0.35f);
        }

        // ---------------- State ----------------

        void OnStateChanged()
        {
            var s = game.State;
            menuRoot.SetActive(s == GameState.Menu);
            if (s == GameState.Menu)
            {
                ShowLevelSelect(false);
                endlessBestText.text = game.EndlessBest > 0 ? $"Best: wave {game.EndlessBest}" : "Survive as long as you can";
            }
            hud.SetActive(s != GameState.Menu);
            debugRoot.SetActive(s != GameState.Menu);
            resultPanel.SetActive(s == GameState.Won || s == GameState.Lost);
            pausePanel.SetActive(false);
            paused = false;
            if (s != GameState.Playing) speed = 1f;

            if (s == GameState.Won || s == GameState.Lost)
            {
                bool won = s == GameState.Won;
                if (game.Endless)
                {
                    int survived = Mathf.Max(0, game.WavesStarted - 1);
                    resultTitle.text = "Base destroyed";
                    resultHeader.color = UIFactory.Warn;
                    for (int i = 0; i < 3; i++) resultStars[i].color = new Color(0f, 0f, 0f, 0.35f);
                    resultStats.text = $"Endless\nWaves survived {survived}    Kills {game.Kills}\nBest this session: wave {game.EndlessBest}";
                    nextLevelButton.gameObject.SetActive(false);
                }
                else
                {
                    bool last = game.LevelIndex == game.Balance.levels.Length - 1;
                    resultTitle.text = won ? (last ? "All missions cleared!" : "Victory!") : "Base destroyed";
                    resultHeader.color = won ? UIFactory.Go : UIFactory.Bad;
                    int stars = won ? game.StarsFor(game.Lives, game.Level.lives) : 0;
                    for (int i = 0; i < 3; i++)
                        resultStars[i].color = i < stars ? Art.Palette.Gold : new Color(0f, 0f, 0f, 0.35f);
                    resultStats.text = $"{game.Level.name}\nWaves {game.WavesStarted}/{game.WaveCount}    Kills {game.Kills}    Lives {game.Lives}";
                    nextLevelButton.gameObject.SetActive(won);
                }
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
            waveText.text = game.Endless ? game.WavesStarted.ToString() : $"{game.WavesStarted}/{game.WaveCount}";
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
            waveButtonLabel.text = game.WavesStarted == 0 ? $"START  {seconds}" : game.Spawning ? "Incoming..." : !all ? $"Next wave  {seconds}" : "";
            waveButtonImage.color = game.CanStartNextWave ? UIFactory.Go : UIFactory.DisabledColor;

            // What the next wave brings: one icon per enemy type with its count.
            var next = game.PeekNextWave();
            previewLabel.text = next == null ? "LAST\nWAVE" : "NEXT";
            int slot = 0;
            if (next != null)
                foreach (var g in next.groups)
                {
                    if (slot >= PreviewSlots) break;
                    previewIcons[slot].sprite = Icons.Enemy(g.enemy);
                    previewCounts[slot].text = "x" + g.count;
                    previewIcons[slot].transform.parent.gameObject.SetActive(true);
                    slot++;
                }
            for (; slot < PreviewSlots; slot++) previewIcons[slot].transform.parent.gameObject.SetActive(false);
        }

        void RefreshBuildPanel()
        {
            var towers = game.Balance.towers;
            for (int i = 0; i < towers.Length; i++)
            {
                bool can = game.CanBuild(towers[i].id);
                ((Image)buildButtons[i].targetGraphic).color = can ? UIFactory.CardColor : UIFactory.DisabledColor;
                buildCosts[i].color = can ? Art.Palette.Gold : Art.Palette.Danger;
            }
        }

        void RefreshTowerPanel(Tower t)
        {
            var type = t.type;
            towerIcon.sprite = Icons.Tower(type.id, t.level);
            towerTitle.text = type.label;
            towerHeader.color = MeshBuilder.Shade(towerColors[type.id], 0.7f);
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

        // Bars use a square-root scale so cheap towers still show a visible bar next to the sniper.
        static void SetStat(StatBar bar, float now, float next, float max, string fmt)
        {
            bar.now.fillAmount = Mathf.Sqrt(now / max);
            bar.next.fillAmount = Mathf.Sqrt(next / max);
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
