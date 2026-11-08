# Bastion Road — store submission brief

Everything needed to fill in **App Store Connect** and **Google Play Console** for this app. Self-contained:
an assistant filling the forms should not need the source code. Facts below were taken from the project and the built
AAB on 2026-11-08. Where a form asks something not covered here, prefer the most conservative truthful answer.

Repository: https://github.com/gvirabyan/game (public). Store assets live in its `Store/` folder.

---

## 1. Identity

| Field | Value |
|---|---|
| App name | **Bastion Road** (fallback if taken on the App Store: `Bastion Road: Tower Defense`) |
| Bundle ID (iOS) / package name (Android) | `com.virabyan.bastionroad` |
| Version (marketing) | `1.0.0` |
| Android versionCode of the current AAB | `4` (every new upload needs a higher number) |
| iOS build number | set automatically by Codemagic (latest App Store/TestFlight build + 1) |
| Developer / seller name | Virabyan |
| Copyright | © 2026 Virabyan |
| Primary language | English (UI also in 15 more languages, see §4) |
| Price | Free, in every country. **No in-app purchases, no subscriptions.** |
| Contains ads | **Yes** |
| Account / login | None. No sign-in of any kind. |
| Internet | Not required to play; ads and analytics need it. |
| Platforms | Android phones (minSdk 26 / Android 8.0, arm64), iPhone only (iOS 15.0+, no iPad build) |
| Orientation | Landscape only (both directions) |

### URLs
| Field | Value |
|---|---|
| Privacy policy | https://gvirabyan.github.io/game/privacy.html |
| Support URL | https://github.com/gvirabyan/game/issues |
| Marketing URL | none (leave empty) |
| Developer website (Play) | none yet — see §12 (app-ads.txt) |

### Service IDs (not secret, for reference)
| Service | Android | iOS |
|---|---|---|
| AdMob App ID | `ca-app-pub-9425467580147795~6839484821` | `ca-app-pub-9425467580147795~3320051370` |
| AdMob interstitial unit | `ca-app-pub-9425467580147795/7775347883` | `ca-app-pub-9425467580147795/6437169303` |
| AdMob rewarded unit | `ca-app-pub-9425467580147795/9372334731` | `ca-app-pub-9425467580147795/4597474022` |
| Firebase project | `bastion-road-td` (account esdu2002@gmail.com) | same |
| Firebase app ID | `1:804166070181:android:1525f74a9dc27c37a7c24c` | `1:804166070181:ios:cbec113e0786d45aa7c24c` |

---

## 2. Builds

- **Android:** `Builds/Android/BastionRoad.aab` (local file on the developer PC, not in git), signed with the upload key.
  Use **Play App Signing** (default). Upload to Internal testing first.
- **iOS:** the Xcode project is exported locally (Unity Personal cannot run in CI) and committed to the
  `ios-export` branch; Codemagic (`codemagic.yaml`) runs `pod install`, signs, builds the IPA and uploads it to
  TestFlight when a tag `v*` is pushed. App Store Connect API key integration: `codemagic`; Apple ID of the app:
  `6832220428`. See "Releasing a new iOS build" below.
- Export compliance (iOS): `ITSAppUsesNonExemptEncryption = NO` is already in Info.plist → answer **"None of the
  algorithms mentioned above" / no non-exempt encryption** (only HTTPS via the OS).

### Releasing a new iOS build
1. On the Windows PC (Unity 6000.6.5f1 with iOS Build Support): `git checkout ios-export && git merge main`, then export:
   `"C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath D:\Corehold -buildTarget iOS -executeMethod BuildScript.BuildIOS -logFile D:\Corehold\ios-export.log`
   and check the log for `[BuildScript] Succeeded: Builds/iOS`.
2. Commit the export on `ios-export`: `git add -A Builds/iOS && git commit -m "iOS export X.Y.Z" && git push`
   (Git LFS takes `*.a` and `UnityRuntime`; `Pods/` and `*.xcworkspace` are ignored, Codemagic creates them).
3. Tag and push: `git tag vX.Y.Z && git push origin vX.Y.Z`. Codemagic builds it and sends it to TestFlight;
   the build number is set automatically (latest + 1). For a new marketing version change
   `PlayerSettings.bundleVersion` in `Assets/Editor/ProjectSetup.cs` (and run ProjectSetup) before exporting.

---

## 3. What the app is

**Genre:** tower defense / strategy. Single player, offline-capable, one-finger controls, landscape.

Enemy convoys drive along a winding dirt road toward the player's headquarters. The player builds towers on fixed pads
beside the road, upgrades or sells them, and calls waves early for speed. Every enemy that reaches the HQ costs lives;
at 0 lives the mission is lost.

- **Campaign:** 100 missions in 4 regions of 25 (grasslands, desert, snow, autumn forest), unlocked in order, up to 3 stars each
  (stars depend on lives kept). A new enemy type joins every 5 missions, a boss fight every 10th mission, towers unlock along the way.
- **Endless mode:** ever-harder generated waves; best result is saved.
- **8 towers**, each with 4 levels (3 upgrades) that also change its look: Machine gun, Cannon, Missiles (anti-air only),
  Flamer, Tesla (chain lightning), Mortar, Sniper, Cryo (slows enemies).
- **17 enemy types** (military vehicles and soldiers, low-poly, no blood): Soldier, Jeep, Drone, Tank, APC, Helicopter,
  Biker, Medic, Heavy tank, Hovercraft, Strike jet, Mech, Carrier, and bosses Juggernaut, Sky Fortress, Titan, Colossus.
  Some fly; some spawn units when destroyed.
- Pinch to zoom, drag to pan, x2 speed, pause, sound on/off, language picker, progress reset.
- **Art and audio:** all 3D models, icons and UI graphics are generated by the game's own code; all sound effects and both
  music tracks are synthesised in code. No asset packs, templates or purchased code. Font: Russo One (SIL Open Font License).
- **Saved data:** only on the device (PlayerPrefs): mission stars, endless best, language, mute.

### Violence description (for rating forms)
Stylised low-poly military combat: towers shoot (bullets, shells, missiles, flame, lightning, frost) at toy-like tanks,
helicopters, drones and small blocky soldier figures. Hit enemies flash and disappear in a small cartoon explosion.
**No blood, no gore, no dismemberment, no realistic humans, no killing of civilians or animals, no player-controlled
human character.** No sexual content, nudity, profanity, drugs, alcohol, tobacco, gambling, simulated gambling,
horror, or user-generated content. No chat or any communication between users. No location sharing.

---

## 4. Languages (UI fully localised)

English, Russian, Armenian, Spanish, French, German, Italian, Portuguese, Polish, Ukrainian, Dutch, Swedish,
Indonesian, Chinese (Simplified), Japanese, Korean. Store texts below exist in English and Russian; other store
localisations can be added later (the game itself already speaks all 16).

---

## 5. Monetisation: ads (exact behaviour)

SDKs: **Google Mobile Ads (AdMob)** 11.6.0 with **Unity Ads** as a mediation network (Google's Unity adapter 3.22.0,
Unity Ads SDK 4.21). Google **UMP** consent SDK. No banners. No other ad networks. No IAP.

1. **First launch:** Google UMP consent form where legally required (EEA, UK, Switzerland, US states with privacy laws).
   On iOS the App Tracking Transparency prompt is shown by UMP (needs the IDFA message configured in AdMob).
   Ads load only after consent is resolved. A **Settings → Privacy** button lets the user change consent (shown where required).
2. **Interstitial:** only when the player leaves a result screen (Next / Retry / Menu after a win or loss). Never during
   gameplay. Not before mission 4, at most once per 2 finished missions and at least 90 seconds apart.
3. **Rewarded (optional):** on the defeat screen, "Watch ad: +5 lives" continues the same fight. Once per attempt.
   Reward only if watched to the end. Never required to progress.
4. While a full-screen ad is shown the game is paused and muted.
5. Ad content rating to request in AdMob: **PG / teens** (not "G only"); app is **not** child-directed
   (no `tagForChildDirectedTreatment`).

---

## 6. Analytics

**Google Analytics for Firebase** (Firebase Unity SDK 14.1.0). Events: `level_start`, `level_end` (success, stars,
waves, kills), `revive`, `ad_interstitial`, `ad_rewarded`, plus Firebase automatic events (first_open, session_start,
app updates, ad_impression when AdMob is linked). Collects the Firebase app instance ID, device model / OS / language /
country (from IP), app version. No names, emails, phone numbers, contacts, photos, files, precise location, health or
financial data. No crash reporting SDK.

---

## 7. Permissions

**Android (from the AAB manifest, all added by Google/Unity SDKs):** INTERNET, ACCESS_NETWORK_STATE, WAKE_LOCK,
FOREGROUND_SERVICE (no typed foreground service permission), BIND_JOB_SERVICE, `com.google.android.gms.permission.AD_ID`,
ACCESS_ADSERVICES_AD_ID / ATTRIBUTION / TOPICS (Privacy Sandbox), install referrer. No camera, microphone, location,
contacts, storage, phone, SMS or notifications permissions.

**iOS:** App Tracking Transparency (`NSUserTrackingUsageDescription`: "Your data will be used to show you more relevant
ads. The game works the same either way."). SKAdNetwork IDs added by the AdMob plugin. No other permission prompts.

---

## 8. Google Play Console answers

### App content
| Section | Answer |
|---|---|
| Privacy policy | https://gvirabyan.github.io/game/privacy.html |
| Ads | **Yes, my app contains ads** |
| App access | All functionality available without special access (no login) |
| Target audience | **13–15, 16–17, 18+** (not under 13). Not designed for children; store listing not aimed at kids. |
| Content rating (IARC) | see below |
| News app | No |
| Government app | No |
| Financial features | None |
| Health | No health features |
| Advertising ID | **Yes**, used for **Advertising or marketing** and **Analytics** |
| COVID / gambling / dating | No |
| Data safety | see below |

### IARC questionnaire (category: Game)
- Violence: **Yes** — fantasy/cartoon violence; characters are stylised (non-realistic) soldiers and vehicles;
  no blood, no gore; violence is not against realistic humans in a realistic way; no torture.
- Fear / horror: No. Sexuality / nudity: No. Language: No profanity. Controlled substances: No.
- Gambling or simulated gambling: No. Loot boxes / random paid items: No.
- User interaction / chat / user-generated content: No. Shares user location: No.
- Digital purchases: No. Ads: Yes (not a rating factor).
- Expected result: roughly PEGI 7 / ESRB Everyone 10+ / USK 6–12 (whatever IARC returns is fine).

### Data safety
- Does the app collect or share user data? **Yes.**
- Is all collected data encrypted in transit? **Yes.**
- Can users request data deletion? **No** (no accounts; data is held by Google/Unity under their policies). If the form
  allows, mention the user can reset the advertising ID in system settings.
- Data types (all **collected and shared**, processed by Google AdMob/Firebase and Unity Ads; **not optional** for the user
  except where consent/ATT applies; purposes **Advertising or marketing** and **Analytics**; also **Fraud prevention,
  security, and compliance** for the ad SDKs):
  - **Location → Approximate location** (derived from IP by the ad SDKs)
  - **App activity → App interactions** (gameplay events, ad interactions)
  - **App info and performance → Diagnostics** (SDK diagnostics)
  - **Device or other IDs** (advertising ID, Firebase app instance ID)
- Not collected: personal info (name, email, user IDs, address, phone), financial info, health, messages, photos/videos,
  audio, files, calendar, contacts, web history, precise location, crash logs, other in-app search history.

### Store listing (English)
- **App name (30):** Bastion Road
- **Short description (80):** Build towers, stop the convoy: 100 missions, 8 towers, bosses and Endless mode.
- **Full description:** use the English description in §10.
- **Category:** Game → **Strategy**. Tags: Tower defense, Strategy, Offline, Casual, Military.
- **Graphics:** icon `Store/play-icon-512.png` (512×512), feature graphic `Store/play-feature-1024x500.png`,
  phone screenshots `Store/screenshots/play-16x9/01–05.png` (1920×1080, landscape).
- Contact email: the developer's email (fill in by hand). Website: optional.

---

## 9. App Store Connect answers

### App information
| Field | Value |
|---|---|
| Name (30) | Bastion Road |
| Subtitle (30) | Hold the road. Defend the base. |
| Primary category | **Games** → subcategories **Strategy**, **Casual** |
| Secondary category | none (or Entertainment) |
| Content rights | Does not contain, show or access third-party content (all assets original; ads are served by AdMob) |
| Age rating | see below |
| Availability | All territories. iPhone only (not offered on iPad; do not tick "available on Mac/Vision Pro" unless wanted) |

### Age rating questionnaire
- Cartoon or Fantasy Violence: **Frequent/Intense** (combat is the core loop, but cartoonish, bloodless).
  Realistic violence: **None**. Prolonged graphic/sadistic violence: **None**. Blood/gore: **None**.
- Sexual content, nudity, profanity, horror/fear themes, drugs/alcohol/tobacco, mature/suggestive themes,
  medical info, gambling, simulated gambling, contests: **None**.
- Unrestricted web access: **No**. User-generated content / messaging: **No**.
- Advertising: **Yes** (if asked). In-app purchases: **No**. Parental controls / age assurance: **No**.
- Made for Kids: **No**.
- Accept whatever rating Apple computes (expected 9+ to 13+).

### App Privacy ("nutrition label")
- Does the app collect data? **Yes.**
- **Data used to track you** (ATT): **Identifiers → Device ID**; **Usage Data → Advertising Data**.
  Purpose: Third-Party Advertising.
- **Data linked to you:** none. **Data not linked to you:**
  - Usage Data → **Product Interaction** (Analytics), **Advertising Data** (Third-Party Advertising, Analytics)
  - Diagnostics → **Other Diagnostic Data** (Analytics, App Functionality)
  - Location → **Coarse Location** (Third-Party Advertising)
  - Identifiers → **Device ID** (also listed under tracking; Analytics, Third-Party Advertising)
- Not collected: contact info, health, financial, precise location, sensitive info, contacts, user content,
  browsing/search history, purchases.

### Version page
- **Promotional text (170):** 100 hand-crafted missions across grasslands, desert, snow and autumn forests. Build, upgrade and combine 8 towers to stop the convoy before it reaches your HQ.
- **Description:** English description in §10.
- **Keywords (100):** `tower,defense,td,strategy,towers,base,army,tank,offline,war,defend,convoy,lowpoly,missions`
- **Support URL / Marketing URL:** see §1.
- **Screenshots:** 6.7"/6.9" iPhone display: `Store/screenshots/iphone-6.7/01–05.png` (2796×1290, landscape).
  No iPad screenshots needed (iPhone-only build).
- **App icon:** inside the build (1024×1024, no transparency).
- **Sign-in required:** No. **Demo account:** not needed.
- **Notes for review:** §11.
- Release: manual or automatic after approval (developer's choice).

---

## 10. Store texts

### English
**Description:**
Enemy convoys are rolling down the road toward your headquarters. Hold the line.

Bastion Road is a classic tower defense game with a clean low-poly look, made for one-handed play in landscape.

• 100 campaign missions across four regions: grasslands, desert, snow and autumn forest
• 8 towers: machine gun, cannon, missiles, flamethrower, tesla, mortar, sniper and cryo — each upgrades and changes its look
• 17 enemy types — soldiers, jeeps, drones, tanks, helicopters, jets, mechs — and a boss every ten missions
• A new enemy every few missions and new towers to unlock as you advance
• Earn up to three stars per mission
• Endless mode: how many waves can you survive?
• Pinch to zoom, speed up waves, play offline
• Free to play, no in-app purchases
• 16 languages

### Русский
**Подзаголовок:** Удержи дорогу. Защити базу.

**Краткое описание (Google Play):** Строй башни и останови колонну: 100 миссий, 8 башен, боссы и бесконечный режим.

**Описание:**
Вражеские колонны движутся по дороге к твоему штабу. Удержи рубеж.

Bastion Road — классическая tower defense в чистом low-poly стиле, для игры одной рукой в горизонтальном режиме.

• 100 миссий кампании в четырёх регионах: луга, пустыня, снега и осенний лес
• 8 башен: пулемёт, пушка, ракеты, огнемёт, тесла, миномёт, снайпер и крио — каждая улучшается и меняет облик
• 17 типов врагов — солдаты, джипы, дроны, танки, вертолёты, самолёты, мехи — и босс каждые десять миссий
• Новые враги и новые башни по мере прохождения
• До трёх звёзд за миссию
• Бесконечный режим: сколько волн ты продержишься?
• Зум щипком, ускорение волн, игра без интернета
• Бесплатно, без покупок
• 16 языков

---

## 11. Notes for App Review (paste as is)

Bastion Road is an original game made from scratch by an independent developer. It uses no templates, purchased
source code or asset packs: all 3D models, UI graphics, sound effects and music are generated procedurally by the
game's own code at startup. Content: a 100-mission campaign (4 regions, 17 enemy types including bosses, 8 upgradable
towers) and an Endless mode. No account or login is needed and the game is fully playable offline.

Ads: AdMob (with Unity Ads mediation). Interstitials appear only between missions (from mission 4, at most every second
mission). An optional rewarded ad on the defeat screen gives +5 lives to continue. The Google UMP consent form and the
App Tracking Transparency prompt are shown before personalised ads. Analytics: Firebase.

How to test: tap mission 1 on the map, tap Play, tap an empty pad to build a tower, tap the green wave button to call
the next wave. Endless mode is the orange button at the top right.

---

## 12. Still to do by the account owner (outside the stores)

1. **Firebase:** enable Google Analytics in project `bastion-road-td` (Project settings → Integrations), then refresh
   `Assets/Firebase/GoogleService-Info.plist` (it currently says IS_ANALYTICS_ENABLED=false).
2. **AdMob:** add Unity Ads to the mediation groups for both ad units; create the GDPR message and the iOS IDFA explainer
   in Privacy & messaging; link the AdMob apps to Firebase; after the apps are live, link them to their store listings.
3. **app-ads.txt:** publish `https://<developer website>/app-ads.txt` with the line from AdMob → Apps → app-ads.txt, and
   put that website into both store listings.
4. **Codemagic:** connect the repo, the `codemagic` API key integration and automatic iOS signing (see §2).
5. **Google Play (new personal accounts):** closed testing with at least 12 testers for 14 days before production access.
