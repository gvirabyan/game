using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Corehold
{
    // AdMob (Google Mobile Ads) with Unity Ads as a mediation network (configured in the AdMob console; the
    // adapter package com.google.ads.mobile.mediation.unity only has to be installed).
    // Flow: UMP consent (GDPR/US states; on iOS it also shows the ATT prompt if the IDFA message is set up in AdMob)
    // -> MobileAds.Initialize -> preload one interstitial and one rewarded ad, reload after each show.
    // Interstitials: only between missions (when leaving the result screen), see balance.json "ads" for frequency.
    // Rewarded: "+lives" revive on the defeat screen.
    // Development builds always use Google's test ad units (clicking your own live ads can get the account banned).
    // In the editor no SDK is touched: rewarded ads are granted instantly so tests and the UI flow still work.
    public sealed class Ads : MonoBehaviour
    {
        // Google's public test units.
        const string TestAndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        const string TestAndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        const string TestIosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        const string TestIosRewarded = "ca-app-pub-3940256099942544/1712485313";
        const float RetryDelay = 30f;

        public static Ads Instance { get; private set; }
        public static event Action Changed;   // ad loaded / consent state changed: UI refreshes buttons

        AdsConfig config;
        InterstitialAd interstitial;
        RewardedAd rewarded;
        bool started, loadingInterstitial, loadingRewarded;
        float retryInterstitialAt = -1f, retryRewardedAt = -1f;
        float lastInterstitialTime = -9999f;
        int resultsSinceInterstitial;

        static bool Live => !Application.isEditor;

        public static void Create(GameObject host, AdsConfig config)
        {
            Instance = host.AddComponent<Ads>();
            Instance.config = config;
            if (Live) Instance.RequestConsent();
        }

        // ---------------- Consent ----------------

        void RequestConsent()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            // Consent from an earlier session: start loading right away while the update runs.
            if (ConsentInformation.CanRequestAds()) StartAds();
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] Consent update failed: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) StartAds();
                    Changed?.Invoke();
                });
            });
        }

        // GDPR requires a way to change the choice later: a "Privacy" button in Settings when this is true.
        public static bool PrivacyOptionsRequired =>
            Live && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public static void ShowPrivacyOptions()
        {
            if (!Live) return;
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning("[Ads] Privacy options: " + error.Message);
                if (ConsentInformation.CanRequestAds()) Instance.StartAds();
            });
        }

        void StartAds()
        {
            if (started) return;
            started = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
        }

        // ---------------- Loading ----------------

        // Development builds: Google test units. Release builds: the ids from balance.json; an empty id switches
        // that ad format off (never ship test units).
        string InterstitialUnit => Unit(config.androidInterstitial, config.iosInterstitial, TestAndroidInterstitial, TestIosInterstitial);
        string RewardedUnit => Unit(config.androidRewarded, config.iosRewarded, TestAndroidRewarded, TestIosRewarded);

        static string Unit(string android, string ios, string testAndroid, string testIos)
        {
            bool iPhone = Application.platform == RuntimePlatform.IPhonePlayer;
            if (Debug.isDebugBuild) return iPhone ? testIos : testAndroid;
            var id = iPhone ? ios : android;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        void LoadInterstitial()
        {
            if (loadingInterstitial || interstitial != null || InterstitialUnit == null) return;
            loadingInterstitial = true;
            InterstitialAd.Load(InterstitialUnit, new AdRequest(), (ad, error) =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] Interstitial load failed: " + error?.GetMessage());
                    retryInterstitialAt = Time.unscaledTime + RetryDelay;
                    return;
                }
                interstitial = ad;
            });
        }

        void LoadRewarded()
        {
            if (loadingRewarded || rewarded != null || RewardedUnit == null) return;
            loadingRewarded = true;
            RewardedAd.Load(RewardedUnit, new AdRequest(), (ad, error) =>
            {
                loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] Rewarded load failed: " + error?.GetMessage());
                    retryRewardedAt = Time.unscaledTime + RetryDelay;
                    return;
                }
                rewarded = ad;
                Changed?.Invoke();
            });
        }

        void Update()
        {
            if (!started) return;
            if (retryInterstitialAt >= 0f && Time.unscaledTime >= retryInterstitialAt) { retryInterstitialAt = -1f; LoadInterstitial(); }
            if (retryRewardedAt >= 0f && Time.unscaledTime >= retryRewardedAt) { retryRewardedAt = -1f; LoadRewarded(); }
        }

        // ---------------- Interstitial ----------------

        // Called once per finished mission / endless run.
        public static void OnResult()
        {
            if (Instance != null) Instance.resultsSinceInterstitial++;
        }

        // Leaving the result screen: maybe show an interstitial, then continue.
        // missionNumber is 1-based; endless runs pass int.MaxValue.
        public static void BetweenMissions(int missionNumber, Action next)
        {
            var ads = Instance;
            if (ads == null || !Live || !ads.InterstitialDue(missionNumber) || ads.interstitial == null || !ads.interstitial.CanShowAd())
            {
                next();
                return;
            }
            var ad = ads.interstitial;
            ads.interstitial = null;
            ads.resultsSinceInterstitial = 0;
            ads.lastInterstitialTime = Time.unscaledTime;
            bool done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                Resume();
                ad.Destroy();
                ads.LoadInterstitial();
                next();
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            Suspend();
            ad.Show();
            Analytics.Event("ad_interstitial");
        }

        bool InterstitialDue(int missionNumber) =>
            missionNumber >= config.interstitialFromMission
            && resultsSinceInterstitial >= config.interstitialEveryResults
            && Time.unscaledTime - lastInterstitialTime >= config.interstitialCooldown;

        // ---------------- Rewarded ----------------

        public static bool RewardedReady => !Live || (Instance != null && Instance.rewarded != null && Instance.rewarded.CanShowAd());

        // onReward runs only if the player watched to the end; onFail if no ad could be shown.
        public static void ShowRewarded(string placement, Action onReward, Action onFail)
        {
            var ads = Instance;
            if (!Live) { onReward(); return; }
            if (ads == null || ads.rewarded == null || !ads.rewarded.CanShowAd())
            {
                ads?.LoadRewarded();
                onFail();
                return;
            }
            var ad = ads.rewarded;
            ads.rewarded = null;
            bool earned = false, done = false;
            void Finish(bool shown)
            {
                if (done) return;
                done = true;
                Resume();
                ad.Destroy();
                ads.LoadRewarded();
                if (earned)
                {
                    Analytics.Event("ad_rewarded", "placement", placement);
                    onReward();
                }
                else if (!shown) onFail();
                Changed?.Invoke();
            }
            ad.OnAdFullScreenContentClosed += () => Finish(true);
            ad.OnAdFullScreenContentFailed += _ => Finish(false);
            Suspend();
            ad.Show(_ => earned = true);
        }

        // Silence the game while a full-screen ad plays (iOS does not pause Unity on its own).
        static float savedTimeScale = 1f;

        static void Suspend()
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        static void Resume()
        {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }
    }
}
