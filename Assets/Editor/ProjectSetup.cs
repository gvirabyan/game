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
    public const string BundleId = "com.virabyan.corehold";

    public static void Run()
    {
        PlayerSettings.companyName = "Virabyan";
        PlayerSettings.productName = "Corehold";
        PlayerSettings.bundleVersion = "0.1.0";
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
        PlayerSettings.iOS.requiresFullScreen = true; // landscape-only game: opt out of iPad multitasking
        PlayerSettings.iOS.hideHomeButton = true;

        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        EditorSettings.serializationMode = SerializationMode.ForceText;

        CreateScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Done");
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
