using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using Minesweeper3D.Unity.LayeredMode;

namespace Minesweeper3D.Editor
{
    /// <summary>
    /// Editor tool to create and configure the LayeredMode scene.
    /// Creates scene with orthographic camera, directional light, and bootstrap object.
    /// </summary>
    public static class LayeredModeSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/LayeredModeScene.unity";

        [MenuItem("Tools/Create LayeredMode Scene")]
        public static void CreateScene()
        {
            // Ensure Scenes directory exists
            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Create new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.18f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            camObj.transform.rotation = Quaternion.identity;
            // Add AudioListener
            camObj.AddComponent<AudioListener>();

            // --- Directional Light ---
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- Bootstrap object ---
            var bootstrapObj = new GameObject("LayeredModeBootstrap");
            bootstrapObj.AddComponent<LayeredModeBootstrap>();
            bootstrapObj.AddComponent<ModeSwitcher>();

            // Save scene
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Add to build settings if not already there
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[LayeredMode] Scene created at {ScenePath} and added to build settings.");
        }

        [MenuItem("Tools/Add Mode Switcher to Current Scene")]
        public static void AddModeSwitcherToCurrentScene()
        {
            // Check if one already exists
            var existing = Object.FindFirstObjectByType<ModeSwitcher>();
            if (existing != null)
            {
                Debug.Log("[LayeredMode] ModeSwitcher already exists in this scene.");
                return;
            }

            var obj = new GameObject("ModeSwitcher");
            obj.AddComponent<ModeSwitcher>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LayeredMode] ModeSwitcher added to {scene.name} and saved.");
        }

        [MenuItem("Tools/Add Mode Switcher to SampleScene")]
        public static void AddModeSwitcherToSampleScene()
        {
            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            if (!File.Exists(sampleScenePath))
            {
                Debug.LogError($"[LayeredMode] {sampleScenePath} not found!");
                return;
            }

            var scene = EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);

            var existing = Object.FindFirstObjectByType<ModeSwitcher>();
            if (existing != null)
            {
                Debug.Log("[LayeredMode] ModeSwitcher already exists in SampleScene.");
                return;
            }

            var obj = new GameObject("ModeSwitcher");
            obj.AddComponent<ModeSwitcher>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LayeredMode] ModeSwitcher added to SampleScene and saved.");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyAdded = false;
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
