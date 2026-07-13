using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class NightShiftQuestBuild
{
    public const string OutputPath = "Builds/Quest3S/NightShiftPrototype.apk";

    [MenuItem("Tools/Night Shift Prototype/Build Quest 3S APK")]
    public static void BuildQuest3SApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android
            && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            throw new UnityEditor.Build.BuildFailedException("Androidへのビルドターゲット切替に失敗しました。");
        }

        ConfigureQuestSettings();
        NightShiftSceneGenerator.GenerateNightShiftPrototype();

        string absoluteOutputPath = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Script/NightShiftPrototype/Scenes/NightShiftTitle.unity",
                "Assets/Scenes/NightShiftPrototype.unity",
                "Assets/Script/NightShiftPrototype/Scenes/NightShiftResult.unity"
            },
            locationPathName = absoluteOutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new UnityEditor.Build.BuildFailedException("Quest 3S APKの生成に失敗しました: " + report.summary.result);

        Debug.Log("Quest 3S APK generated: " + absoluteOutputPath);
    }

    public static void BuildQuest3SBatch()
    {
        BuildQuest3SApk();
    }

    private static void ConfigureQuestSettings()
    {
        PlayerSettings.companyName = "NightShiftPrototype";
        PlayerSettings.productName = "夜勤警備";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.nightshift.prototype");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.buildAppBundle = false;
    }
}
