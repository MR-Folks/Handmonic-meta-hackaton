using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Builds the app for Quest.
    /// <list type="bullet">
    /// <item><see cref="BuildApk"/> writes Builds/MusicInYourHands.apk, signed with this computer's debug key, for
    /// installing with Meta Quest Developer Hub or "adb install -r Builds/MusicInYourHands.apk".</item>
    /// <item><see cref="BuildReleaseApk"/> writes Builds/MusicInYourHands-release.apk, signed with the team's
    /// release keystore, for uploading to a release channel on the Meta Horizon Developer Dashboard. Run it
    /// through Tools/release/build-release.ps1, which asks for the password.</item>
    /// </list>
    /// Batch mode: -executeMethod MusicInYourHands.EditorTools.QuestBuild.BuildApk (or BuildReleaseApk)
    /// </summary>
    public static class QuestBuild
    {
        const string OutputPath = "Builds/MusicInYourHands.apk";
        const string ReleaseOutputPath = "Builds/MusicInYourHands-release.apk";

        // The release keystore never goes into git, and neither do its settings: a committed
        // androidUseCustomKeystore would break everyone else's debug builds. So the release build reads them
        // from the environment and puts the player settings back once it is done.
        const string KeystoreVariable = "FTM_KEYSTORE";            // path to the .keystore file
        const string KeystorePassVariable = "FTM_KEYSTORE_PASS";   // keystore password
        const string AliasVariable = "FTM_KEY_ALIAS";              // optional, default DefaultAlias
        const string KeyPassVariable = "FTM_KEY_PASS";             // optional, default the keystore password
        const string VersionCodeVariable = "FTM_VERSION_CODE";     // optional, default from the build time
        const string DefaultAlias = "musicinyourhands";

        [MenuItem("Music In Your Hands/Build APK for Quest")]
        public static void BuildApk()
        {
            var result = Build(OutputPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Music In Your Hands/Build Release APK for Quest (signed)")]
        public static void BuildReleaseApk()
        {
            var result = BuildRelease();
            if (Application.isBatchMode)
                EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildResult BuildRelease()
        {
            var keystore = Environment.GetEnvironmentVariable(KeystoreVariable);
            var storePass = Environment.GetEnvironmentVariable(KeystorePassVariable);
            var alias = Variable(AliasVariable, DefaultAlias);
            var keyPass = Variable(KeyPassVariable, storePass);
            // Gradle runs as a child of this process with its environment, and a failed Gradle run prints that whole
            // environment into the build log. Forget the passwords' variables before building.
            Environment.SetEnvironmentVariable(KeystorePassVariable, null);
            Environment.SetEnvironmentVariable(KeyPassVariable, null);
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore) || string.IsNullOrEmpty(storePass))
            {
                var message = $"[Build] Release build needs {KeystoreVariable} (an existing keystore file) and " +
                              $"{KeystorePassVariable}. Run Tools/release/build-release.ps1, which asks for them.";
                Debug.LogError(message);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Release build", message, "OK");
                return BuildResult.Failed;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable(VersionCodeVariable), out var versionCode))
                versionCode = TimeVersionCode(DateTime.UtcNow);

            var android = new
            {
                PlayerSettings.Android.useCustomKeystore,
                PlayerSettings.Android.bundleVersionCode,
                // Read raw: putting back an empty keystoreName through its setter would save '{inproject}: '.
                Keystore = PlayerSettingsAsset().FindProperty("AndroidKeystoreName")?.stringValue,
                Alias = PlayerSettingsAsset().FindProperty("AndroidKeyaliasName")?.stringValue
            };
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                Debug.Log($"[Build] Release: version {PlayerSettings.bundleVersion} ({versionCode}), alias {alias}");
                return Build(ReleaseOutputPath);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = android.useCustomKeystore;
                PlayerSettings.Android.bundleVersionCode = android.bundleVersionCode;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                var settings = PlayerSettingsAsset();
                SetString(settings, "AndroidKeystoreName", android.Keystore);
                SetString(settings, "AndroidKeyaliasName", android.Alias);
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static SerializedObject PlayerSettingsAsset() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);

        static void SetString(SerializedObject settings, string name, string value)
        {
            var property = settings.FindProperty(name);
            if (property != null && value != null)
                property.stringValue = value;
        }

        /// <summary>
        /// A version code that only goes up: the UTC date and hour, then the tenth of the hour
        /// (2026-11-18 11:54 is 261118119). The Developer Dashboard refuses a build whose version code isn't
        /// higher than every build uploaded before it.
        /// </summary>
        static int TimeVersionCode(DateTime utc) => int.Parse(utc.ToString("yyMMddHH")) * 10 + utc.Minute / 6;

        static string Variable(string name, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        static BuildResult Build(string outputPath)
        {
            // Meta's build step only strips the duplicate OpenXR loader when Android is the *selected* target group
            // too; in command-line builds that can lag behind the active target, so set it explicitly.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            return summary.result;
        }
    }
}
