#if FIREBASE_CONFIGURED && !UNITY_EDITOR
#define FIREBASE_LIVE
#endif
using UnityEngine;
#if FIREBASE_LIVE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace Corehold
{
    // Firebase (Google) Analytics. Live only on devices AND when the build had a Firebase config file
    // (BuildScript sets FIREBASE_CONFIGURED if Assets/Firebase/google-services.json or GoogleService-Info.plist exists);
    // otherwise every call is a no-op, so the game runs fine without a Firebase project.
    // Events are sent at level boundaries only (never per frame). AdMob's ad_impression is logged automatically
    // once the AdMob app is linked to Firebase.
    public static class Analytics
    {
#if FIREBASE_LIVE
        static bool ready;
#endif

        public static void Init()
        {
#if FIREBASE_LIVE
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning("[Analytics] Firebase unavailable: " + task.Result);
                    return;
                }
                try
                {
                    _ = FirebaseApp.DefaultInstance;
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    ready = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Analytics] Firebase init failed: " + e.Message);
                }
            });
#endif
        }

        public static void LevelStart(string level)
        {
#if FIREBASE_LIVE
            if (!ready) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, FirebaseAnalytics.ParameterLevelName, level);
#endif
        }

        public static void LevelEnd(string level, bool success, int stars, int waves, int kills)
        {
#if FIREBASE_LIVE
            if (!ready) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd,
                new Parameter(FirebaseAnalytics.ParameterLevelName, level),
                new Parameter(FirebaseAnalytics.ParameterSuccess, success ? 1L : 0L),
                new Parameter("stars", stars),
                new Parameter("waves", waves),
                new Parameter("kills", kills));
#endif
        }

        public static void Event(string name, string param = null, string value = null)
        {
#if FIREBASE_LIVE
            if (!ready) return;
            if (param == null) FirebaseAnalytics.LogEvent(name);
            else FirebaseAnalytics.LogEvent(name, param, value);
#endif
        }
    }
}
