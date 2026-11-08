using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line builds. Run with the matching -buildTarget so Unity does not have to switch platforms mid-run.
//   Unity.exe -batchmode -quit -buildTarget Android -projectPath . -executeMethod BuildScript.BuildAndroidDebug -logFile -
public static class BuildScript
{
    // Development build: profiler can attach, shows "Development Build" watermark.
    public static void BuildAndroidDebug() =>
        BuildAndroid("Builds/Android/Corehold-debug.apk", BuildOptions.Development, false);

    // Non-development build signed with the debug key; use this one to judge fps on a phone.
    public static void BuildAndroidPerf() =>
        BuildAndroid("Builds/Android/Corehold-perf.apk", BuildOptions.None, false);

    // Google Play upload: Builds/Android/BastionRoad.aab signed with the upload key.
    // Env: ANDROID_KEYSTORE (path), ANDROID_KEYSTORE_PASS, ANDROID_KEY_ALIAS, ANDROID_KEY_PASS (defaults to the store password),
    //      BUILD_NUMBER (versionCode; must grow with every upload, otherwise the current Player Settings value is used).
    public static void BuildAndroidRelease()
    {
        var keystore = Env("ANDROID_KEYSTORE");
        if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
        {
            Debug.LogError("[BuildScript] Failed: set ANDROID_KEYSTORE to the upload keystore path");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keystorePass = Env("ANDROID_KEYSTORE_PASS");
        PlayerSettings.Android.keyaliasName = Env("ANDROID_KEY_ALIAS") ?? "upload";
        PlayerSettings.Android.keyaliasPass = Env("ANDROID_KEY_PASS") ?? Env("ANDROID_KEYSTORE_PASS");
        if (int.TryParse(Env("BUILD_NUMBER"), out var code)) PlayerSettings.Android.bundleVersionCode = code;
        try
        {
            BuildAndroid("Builds/Android/BastionRoad.aab", BuildOptions.CleanBuildCache, true); // releases always from scratch
        }
        finally
        {
            // Never leave signing secrets in ProjectSettings.asset.
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
            AssetDatabase.SaveAssets();
        }
    }

    // Release Xcode project for the App Store (built on a Mac, e.g. Codemagic):
    //   Unity -batchmode -quit -buildTarget iOS -projectPath . -executeMethod BuildScript.BuildIOS -logFile -
    // Output: Builds/iOS (Unity-iPhone.xcodeproj). CFBundleVersion comes from $BUILD_NUMBER (set by Codemagic) when present.
    public static void BuildIOS()
    {
        const string path = "Builds/iOS";
        var build = Env("BUILD_NUMBER");
        if (!string.IsNullOrEmpty(build)) PlayerSettings.iOS.buildNumber = build;
        PrepareSdks(BuildTarget.iOS);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ProjectSetup.ScenePath },
            locationPathName = path,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"[BuildScript] {summary.result}: {path} (build {PlayerSettings.iOS.buildNumber}, {summary.totalErrors} errors, {summary.totalTime})");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // Firebase config files (from the Firebase console) go here; without them analytics is compiled out.
    const string FirebaseAndroidConfig = "Assets/Firebase/google-services.json";
    const string FirebaseIosConfig = "Assets/Firebase/GoogleService-Info.plist";

    // Before every player build: FIREBASE_CONFIGURED define per platform, and (Android) resolve the Gradle
    // dependencies of AdMob, the Unity Ads adapter and Firebase into the custom Gradle templates.
    static void PrepareSdks(BuildTarget target)
    {
        bool ios = target == BuildTarget.iOS;
        var named = ios ? UnityEditor.Build.NamedBuildTarget.iOS : UnityEditor.Build.NamedBuildTarget.Android;
        bool firebase = File.Exists(ios ? FirebaseIosConfig : FirebaseAndroidConfig);
        PlayerSettings.GetScriptingDefineSymbols(named, out var defines);
        var list = new System.Collections.Generic.List<string>(defines);
        list.Remove("FIREBASE_CONFIGURED");
        if (firebase) list.Add("FIREBASE_CONFIGURED");
        PlayerSettings.SetScriptingDefineSymbols(named, list.ToArray());
        Debug.Log($"[BuildScript] Firebase analytics {(firebase ? "enabled" : "disabled (no config file)")} for {target}");

        if (!ios)
        {
            var resolver = Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
            var resolve = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
            if (resolve == null) Debug.LogWarning("[BuildScript] EDM PlayServicesResolver.ResolveSync not found; relying on resolve-on-build");
            else Debug.Log("[BuildScript] Android dependencies resolved: " + resolve.Invoke(null, new object[] { true }));
        }
    }

    static string Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    static void BuildAndroid(string path, BuildOptions options, bool appBundle)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorUserBuildSettings.buildAppBundle = appBundle;
        PrepareSdks(BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ProjectSetup.ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            options = options
        });
        EditorUserBuildSettings.buildAppBundle = false;

        var summary = report.summary;
        Debug.Log($"[BuildScript] {summary.result}: {path} (versionCode {PlayerSettings.Android.bundleVersionCode}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime})");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

#if UNITY_IOS
// The game uses no encryption beyond what iOS provides, so App Store Connect stops asking about export compliance.
static class IOSPostBuild
{
    [UnityEditor.Callbacks.PostProcessBuild]
    static void OnPostBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new UnityEditor.iOS.Xcode.PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
