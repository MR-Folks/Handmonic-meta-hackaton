using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Builds Builds/MusicInYourHands.apk for Quest. Install with Meta Quest Developer Hub or
    /// "adb install -r Builds/MusicInYourHands.apk".
    /// Batch mode: -executeMethod MusicInYourHands.EditorTools.QuestBuild.BuildApk
    /// </summary>
    public static class QuestBuild
    {
        const string OutputPath = "Builds/MusicInYourHands.apk";

        [MenuItem("Music In Your Hands/Build APK for Quest")]
        public static void BuildApk()
        {
            // Meta's build step only strips the duplicate OpenXR loader when Android is the *selected* target group
            // too; in command-line builds that can lag behind the active target, so set it explicitly.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {OutputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
