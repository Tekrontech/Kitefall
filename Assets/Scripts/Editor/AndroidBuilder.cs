using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Kitefall ▸ Build: one-click Android builds.
    /// APK = install directly on a phone (testing). AAB = upload to Google Play (cannot be installed directly).
    /// Output goes to &lt;project&gt;/Builds/Android (ignored by git).
    /// </summary>
    public static class AndroidBuilder
    {
        public const string DefaultPackageName = "com.tekrontech.kitefall";
        private const string OutputFolder = "Builds/Android";

        [MenuItem("Kitefall/Build/Android APK (install on phone)", priority = 50)]
        public static void BuildApk() => Run(appBundle: false, development: false, runOnPhone: false);

        [MenuItem("Kitefall/Build/Android APK – Development (shows errors)", priority = 51)]
        public static void BuildApkDevelopment() => Run(appBundle: false, development: true, runOnPhone: false);

        [MenuItem("Kitefall/Build/Android APK – Build and Run on USB phone", priority = 52)]
        public static void BuildApkAndRun() => Run(appBundle: false, development: true, runOnPhone: true);

        [MenuItem("Kitefall/Build/Android AAB (Google Play upload)", priority = 70)]
        public static void BuildAab() => Run(appBundle: true, development: false, runOnPhone: false);

        [MenuItem("Kitefall/Build/Open Builds Folder", priority = 90)]
        public static void OpenFolder()
        {
            string dir = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static void Run(bool appBundle, bool development, bool runOnPhone)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Kitefall", "Stop Play mode first.", "OK"); return; }
            if (EditorApplication.isCompiling) { EditorUtility.DisplayDialog("Kitefall", "Unity is still compiling scripts. Try again in a moment.", "OK"); return; }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Android module missing",
                    "Install it in Unity Hub ▸ Installs ▸ (this Unity version) ⚙ ▸ Add modules ▸ Android Build Support + OpenJDK + Android SDK & NDK Tools. Then restart Unity.", "OK");
                return;
            }
            if (appBundle && !PlayerSettings.Android.useCustomKeystore &&
                !EditorUtility.DisplayDialog("No release keystore",
                    "Google Play does not accept an AAB signed with the debug key.\n\n" +
                    "Create one in Project Settings ▸ Player ▸ Android ▸ Publishing Settings ▸ Keystore Manager (save it OUTSIDE the project and never share the password).\n\n" +
                    "Build anyway (debug-signed, for checking only)?", "Build anyway", "Cancel"))
                return;
            if (!SigningReady()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Kitefall] Could not switch to Android. Use File ▸ Build Profiles ▸ Android ▸ Switch Platform, then try again.");
                return;
            }

            if (!GameSceneBuilder.LinkMainScene()) return;
            ApplyAndroidSettings(appBundle);

            string ext = appBundle ? "aab" : "apk";
            string version = PlayerSettings.bundleVersion;
            string file = $"Kitefall-{version}-{PlayerSettings.Android.bundleVersionCode}{(development ? "-dev" : "")}.{ext}";
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, file);

            EditorUserBuildSettings.buildAppBundle = appBundle;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { GameSceneBuilder.MainScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = (development ? BuildOptions.Development : BuildOptions.None) | (runOnPhone ? BuildOptions.AutoRunPlayer : BuildOptions.None)
            };

            Debug.Log($"[Kitefall] Building {ext.ToUpper()} → {path} (first build can take 10–30 minutes)…");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[Kitefall] Build succeeded: {Path.GetFullPath(path)} ({mb:0.0} MB, {summary.totalTime:mm\\:ss}).");
                if (!runOnPhone) EditorUtility.RevealInFinder(Path.GetFullPath(path));
                // Every build gets a higher version code: Play needs it, and a phone refuses ("App not installed") an APK whose
                // code is lower than the one already installed.
                PlayerSettings.Android.bundleVersionCode++;
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError($"[Kitefall] Build {summary.result}: {summary.totalErrors} error(s). Scroll up in the Console for the first red error.");
                EditorUtility.DisplayDialog("Build failed", "See the first red error in the Console (scroll up). Send it to get help.", "OK");
            }
        }

        /// <summary>Settings Google Play requires (spec: Android first, landscape). Only fills defaults – your own values are kept.</summary>
        private static void ApplyAndroidSettings(bool appBundle)
        {
            ProjectSetup.ApplyMobilePlayerSettings();
            var android = NamedBuildTarget.Android;

            string id = PlayerSettings.GetApplicationIdentifier(android);
            if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id.Contains("Template"))
                PlayerSettings.SetApplicationIdentifier(android, DefaultPackageName);
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "Tekrontech";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "0.1.0")
                PlayerSettings.bundleVersion = "1.0.0";

            if ((int)PlayerSettings.Android.minSdkVersion < 24) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            // 64-bit AND 32-bit: many budget phones (Android Go, older Tecno / Infinix / itel / Samsung A) run a 32-bit system and
            // show "App not installed" for an ARM64-only APK. Google Play needs ARM64; with an AAB each phone gets only its own part.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Unity never saves keystore passwords, so after every restart they are empty and Gradle fails at the very end with
        /// "keystore password was incorrect". Check before the long build and open the right settings page instead.
        /// </summary>
        private static bool SigningReady()
        {
            if (!PlayerSettings.Android.useCustomKeystore) return true;
            bool missingFile = string.IsNullOrEmpty(PlayerSettings.Android.keystoreName) || !File.Exists(PlayerSettings.Android.keystoreName);
            bool missingPass = string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass);
            if (!missingFile && !missingPass) return true;
            EditorUtility.DisplayDialog("Keystore password needed",
                (missingFile ? "The keystore file was not found: " + PlayerSettings.Android.keystoreName + "\n\n" : "") +
                "Unity forgets keystore passwords every time it restarts.\n\n" +
                "Player ▸ Android ▸ Publishing Settings will open now: type the Keystore password and the Project Key (alias) password, " +
                "then build again. Never put the password in code or chat.", "Open settings");
            SettingsService.OpenProjectSettings("Project/Player");
            return false;
        }
    }
}
