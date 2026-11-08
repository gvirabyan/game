using Corehold;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Applies project settings and (re)creates the bootstrap scene. Safe to run repeatedly:
// Unity.exe -batchmode -quit -projectPath . -executeMethod ProjectSetup.Run -logFile -
public static class ProjectSetup
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    public const string BundleId = "com.virabyan.bastionroad";
    public const string DisplayName = "Bastion Road";

    // AdMob app ids (ad unit ids are in balance.json "ads"). Empty id = Google's test app id.
    public const string AdMobIosAppId = "ca-app-pub-9425467580147795~3320051370";
    public const string AdMobAndroidAppId = "ca-app-pub-9425467580147795~6839484821";
    const string AdMobTestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
    const string AdMobTestIosAppId = "ca-app-pub-3940256099942544~1458002511";
    const string TrackingDescription = "Your data will be used to show you more relevant ads. The game works the same either way.";

    public static void Run()
    {
        PlayerSettings.companyName = "Virabyan";
        PlayerSettings.productName = DisplayName;
        PlayerSettings.bundleVersion = "1.0.0"; // store version; build numbers come from BuildScript
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

        // Landscape only; the phone may be turned either way.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly; // landscape phone game; no iPad screenshots needed
        PlayerSettings.iOS.requiresFullScreen = true; // landscape-only game: opt out of iPad multitasking
        PlayerSettings.iOS.hideHomeButton = true;

        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        EditorSettings.serializationMode = SerializationMode.ForceText;

        ConfigureAdMob();
        CopyGradleTemplates();
        CreateScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Done");
    }

    // Google Mobile Ads keeps its app ids in an internal ScriptableObject (Assets/GoogleMobileAds/Resources);
    // fill it through reflection + SerializedObject so it never needs the Inspector.
    static void ConfigureAdMob()
    {
        var type = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
        var load = type?.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (load == null)
        {
            Debug.LogError("[ProjectSetup] GoogleMobileAdsSettings not found: is com.google.ads.mobile installed?");
            return;
        }
        var settings = new SerializedObject((Object)load.Invoke(null, null));
        settings.FindProperty("adMobAndroidAppId").stringValue = string.IsNullOrEmpty(AdMobAndroidAppId) ? AdMobTestAndroidAppId : AdMobAndroidAppId;
        settings.FindProperty("adMobIOSAppId").stringValue = string.IsNullOrEmpty(AdMobIosAppId) ? AdMobTestIosAppId : AdMobIosAppId;
        settings.FindProperty("userTrackingUsageDescription").stringValue = TrackingDescription; // iOS ATT prompt text
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings.targetObject);
    }

    // Custom Gradle templates let the External Dependency Manager add the AdMob/Unity Ads/Firebase Android
    // libraries to the Gradle build (it patches mainTemplate.gradle and settingsTemplate.gradle).
    static void CopyGradleTemplates()
    {
        const string dst = "Assets/Plugins/Android";
        var src = System.IO.Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/Tools/GradleTemplates");
        System.IO.Directory.CreateDirectory(dst);
        foreach (var file in new[] { "mainTemplate.gradle", "gradleTemplate.properties", "settingsTemplate.gradle" })
        {
            var target = System.IO.Path.Combine(dst, file);
            if (!System.IO.File.Exists(target)) System.IO.File.Copy(System.IO.Path.Combine(src, file), target);
        }
        AssetDatabase.Refresh();
    }

    static void CreateScene()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Bootstrap").AddComponent<Bootstrap>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
