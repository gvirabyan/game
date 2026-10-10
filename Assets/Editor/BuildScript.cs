using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line builds. Run with -buildTarget Android so Unity does not have to switch platforms mid-run.
//   Unity.exe -batchmode -quit -buildTarget Android -projectPath . -executeMethod BuildScript.BuildAndroidDebug -logFile -
public static class BuildScript
{
    // Development build: profiler can attach, shows "Development Build" watermark.
    public static void BuildAndroidDebug() =>
        BuildAndroid("Builds/Android/Corehold-debug.apk", BuildOptions.Development);

    // Non-development build signed with the debug key; use this one to judge fps on a phone.
    public static void BuildAndroidPerf() =>
        BuildAndroid("Builds/Android/Corehold-perf.apk", BuildOptions.None);

    static void BuildAndroid(string path, BuildOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorUserBuildSettings.buildAppBundle = false;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ProjectSetup.ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            options = options
        });

        var summary = report.summary;
        Debug.Log($"[BuildScript] {summary.result}: {path} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime})");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
