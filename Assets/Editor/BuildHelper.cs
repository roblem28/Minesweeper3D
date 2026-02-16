using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Minesweeper3D.Editor
{
    public static class BuildHelper
    {
        [MenuItem("Tools/Switch to Android")]
        public static void SwitchToAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[BuildHelper] Switched active build target to Android");
        }

        [MenuItem("Tools/Build Android APK")]
        public static void BuildAndroidAPK()
        {
            string outputDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Builds");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string apkPath = Path.Combine(outputDir, "MineSweep3D.apk");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildHelper] No scenes in Build Settings! Add scenes first.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            Debug.Log($"[BuildHelper] Building APK to {apkPath} with {scenes.Length} scene(s)...");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[BuildHelper] Build succeeded: {apkPath} ({report.summary.totalSize / (1024 * 1024)}MB)");
            else
                Debug.LogError($"[BuildHelper] Build failed: {report.summary.result}");
        }
    }
}
