# Bastion Road — store listing

Graphics in this folder: `play-icon-512.png`, `play-feature-1024x500.png`, `screenshots/iphone-6.7/*` (App Store, 2796x1290),
`screenshots/play-16x9/*` (Google Play, 1920x1080). The App Store icon is inside the build (Assets/Icons/AppIcon.png).
Regenerate: `-executeMethod AppIcon.Generate` (icon) and `-testFilter Corehold.Tests.ScreenshotTests.CaptureStoreScreenshots` (screenshots).

## Common
- Name: **Bastion Road** (App Store name field: `Bastion Road: Tower Defense` if "Bastion Road" alone is taken)
- Bundle id / package: `com.virabyan.bastionroad`
- Category: Games → Strategy (secondary: Casual)
- Price: Free, contains ads (AdMob + Unity Ads via mediation). No in-app purchases.
- Privacy policy URL: https://gvirabyan.github.io/game/privacy.html
- Support URL: https://github.com/gvirabyan/game/issues
- Age rating: 9+ (App Store: "Infrequent/Mild Cartoon or Fantasy Violence"); Google Play IARC questionnaire:
  violence — yes, mild/cartoon, no blood, no humans harmed realistically; everything else — no.
- Ads: **Yes** (Play "Contains ads"). Target audience (Play): 13+ — not for children (keeps AdMob/Families rules simple).
- Google Play Data safety (AdMob + Firebase Analytics): collected and shared — **Device or other IDs** (advertising ID),
  **App activity → App interactions**, **App info and performance → Diagnostics**, **Location → Approximate** (from IP, by ads).
  Purposes: Advertising or marketing, Analytics. Encrypted in transit: yes. Users can request deletion: no (no account).
- App Store App Privacy: Data used to track you — **Identifiers → Device ID**, **Usage Data → Advertising Data**;
  Data not linked to you — **Usage Data → Product Interaction**, **Diagnostics**, **Location → Coarse**.
  Purposes: Third-Party Advertising, Analytics. Tracking: yes (ATT prompt is shown).
- app-ads.txt: AdMob wants `https://<developer website>/app-ads.txt` with the line from AdMob → Apps → app-ads.txt.

## English
**Subtitle (30):** Hold the road. Defend the base.

**Promotional text (170):** 100 hand-crafted missions across grasslands, desert, snow and autumn forests. Build, upgrade and combine 8 towers to stop the convoy before it reaches your HQ.

**Short description — Google Play (80):** Build towers, stop the convoy: 100 missions, 8 towers, bosses and Endless mode.

**Description:**
Enemy convoys are rolling down the road toward your headquarters. Hold the line.

Bastion Road is a classic tower defense game with a clean low-poly look, made for one-handed play in landscape.

• 100 campaign missions across four regions: grasslands, desert, snow and autumn forest
• 8 towers: machine gun, cannon, missiles, flamethrower, tesla, mortar, sniper and cryo — each upgrades and changes its look
• Soldiers, jeeps, drones, tanks, APCs, helicopters and a boss every ten missions
• A new enemy every few missions and new towers to unlock as you advance
• Earn up to three stars per mission
• Endless mode: how many waves can you survive?
• Pinch to zoom, speed up waves, play offline
• Free to play, no in-app purchases
• 16 languages

**Keywords — App Store (100):** tower,defense,td,strategy,towers,base,army,tank,offline,war,defend,convoy,lowpoly,missions

## Русский
**Подзаголовок:** Удержи дорогу. Защити базу.

**Краткое описание (Google Play):** Строй башни и останови колонну: 100 миссий, 8 башен, боссы и бесконечный режим.

**Описание:**
Вражеские колонны движутся по дороге к твоему штабу. Удержи рубеж.

Bastion Road — классическая tower defense в чистом low-poly стиле, для игры одной рукой в горизонтальном режиме.

• 100 миссий кампании в четырёх регионах: луга, пустыня, снега и осенний лес
• 8 башен: пулемёт, пушка, ракеты, огнемёт, тесла, миномёт, снайпер и крио — каждая улучшается и меняет облик
• Солдаты, джипы, дроны, танки, БТР, вертолёты и босс каждые десять миссий
• Новые враги и новые башни по мере прохождения
• До трёх звёзд за миссию
• Бесконечный режим: сколько волн ты продержишься?
• Зум щипком, ускорение волн, игра без интернета
• Бесплатно, без покупок
• 16 языков

## App Review notes (App Store)
Bastion Road is an original game made from scratch by an independent developer. It uses no templates, purchased
source code or asset packs: all 3D models, UI graphics, sound effects and music are generated procedurally by the
game's own code at startup. Content: a 100-mission campaign (4 regions, 7 enemy types, bosses, 8 upgradable towers)
and an Endless mode. No account or login is needed and the game is fully playable offline. It shows AdMob ads (interstitials only between
missions, an optional rewarded "continue" on the defeat screen) after the Google UMP consent form, and uses Firebase Analytics.
How to test: tap mission 1 on the map, tap Play, tap an empty pad to build a tower, tap the green wave button to call the next wave.
