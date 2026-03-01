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

        [MenuItem("Tools/Set App Icon")]
        public static void SetAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/icon.png");
            if (icon == null)
            {
                Debug.LogError("[BuildHelper] Icon not found at Assets/Icons/icon.png");
                return;
            }

            // Set the icon texture as readable and proper import settings
            var importer = AssetImporter.GetAtPath("Assets/Icons/icon.png") as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/icon.png");
            }

            // Set default icon (all platforms)
            var icons = new Texture2D[] { icon };
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, icons);

            // Set Android adaptive icon (foreground)
            var namedTarget = UnityEditor.Build.NamedBuildTarget.Android;
            var kinds = PlayerSettings.GetSupportedIconKinds(namedTarget);
            foreach (var kind in kinds)
            {
                var iconSizes = PlayerSettings.GetPlatformIcons(namedTarget, kind);
                foreach (var iconSize in iconSizes)
                {
                    for (int layer = 0; layer < iconSize.layerCount; layer++)
                        iconSize.SetTexture(icon, layer);
                }
                PlayerSettings.SetPlatformIcons(namedTarget, kind, iconSizes);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildHelper] App icon set successfully");
        }

        [MenuItem("Tools/Build Android APK")]
        public static void BuildAndroidAPK()
        {
            SetAppIcon();
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
                options = BuildOptions.None
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
