using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

namespace Minesweeper3D.Editor
{
    public static class SplashSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/SplashScene.unity";

        [MenuItem("Tools/Create Splash Scene")]
        public static void CreateScene()
        {
            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.16f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camObj.transform.position = new Vector3(0f, 0f, -8f);
            camObj.AddComponent<AudioListener>();

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Bootstrap
            var bootstrapObj = new GameObject("SplashBootstrap");
            bootstrapObj.AddComponent<Minesweeper3D.Unity.SplashController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            InsertSceneAtBuildIndex0(ScenePath);

            Debug.Log($"[Splash] Scene created at {ScenePath} and inserted at build index 0.");
        }

        [MenuItem("Tools/Remove Splash Scene from Build")]
        public static void RemoveFromBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Splash] Removed SplashScene from build settings.");
        }

        private static void InsertSceneAtBuildIndex0(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Remove if already present
            scenes.RemoveAll(s => s.path == scenePath);

            // Insert at index 0
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
