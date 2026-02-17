using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace Minesweeper3D.Editor
{
    public static class MaterialCreator
    {
        private const string OutputFolder = "Assets/Resources/Materials";

        [MenuItem("Tools/Create & Assign Materials")]
        public static void CreateAndAssignMaterials()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            // Find URP shaders (available in editor)
            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (simpleLit == null)
                simpleLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

            if (simpleLit == null || unlit == null)
            {
                Debug.LogError("MaterialCreator: URP shaders not found. Is URP installed?");
                return;
            }

            // --- Opaque material for active cubes ---
            Material opaque;
            {
                var mat = new Material(simpleLit);
                mat.SetColor("_BaseColor", new Color(0.44f, 0.44f, 0.44f, 1f));
                mat.color = new Color(0.44f, 0.44f, 0.44f, 1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_SpecColor"))
                    mat.SetColor("_SpecColor", new Color(0.1f, 0.1f, 0.1f, 1f));
                SaveMat(mat, "Opaque");
                opaque = AssetDatabase.LoadAssetAtPath<Material>($"{OutputFolder}/Opaque.mat");
            }

            // --- Transparent material for ghost wireframes ---
            Material ghost;
            {
                var mat = new Material(unlit);
                ConfigureTransparent(mat);
                mat.SetColor("_BaseColor", new Color(1.00f, 1.00f, 0.00f, 0.30f));
                mat.color = new Color(1.00f, 1.00f, 0.00f, 0.30f);
                SaveMat(mat, "Ghost");
                ghost = AssetDatabase.LoadAssetAtPath<Material>($"{OutputFolder}/Ghost.mat");
            }

            // --- Floor material (transparent dark blue-gray) ---
            Material floor;
            {
                var mat = new Material(simpleLit);
                mat.SetColor("_BaseColor", new Color(0.4f, 0.53f, 0.73f, 1f));
                mat.color = new Color(0.4f, 0.53f, 0.73f, 1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                SaveMat(mat, "Floor");
                floor = AssetDatabase.LoadAssetAtPath<Material>($"{OutputFolder}/Floor.mat");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MaterialCreator: Created Opaque.mat, Ghost.mat, Floor.mat in " + OutputFolder);

            // --- Auto-assign to GameController in the active scene ---
            var gc = Object.FindFirstObjectByType<Minesweeper3D.Unity.GameController>();
            if (gc == null)
            {
                // Try FindObjectOfType as fallback (older Unity API)
                gc = Object.FindObjectOfType<Minesweeper3D.Unity.GameController>();
            }

            if (gc != null)
            {
                var so = new SerializedObject(gc);
                so.FindProperty("opaqueMaterial").objectReferenceValue = opaque;
                so.FindProperty("ghostMaterial").objectReferenceValue = ghost;
                so.FindProperty("floorMaterial").objectReferenceValue = floor;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(gc);
                EditorSceneManager.MarkSceneDirty(gc.gameObject.scene);
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("MaterialCreator: Assigned materials to GameController and saved scene.");
            }
            else
            {
                Debug.LogWarning("MaterialCreator: GameController not found in scene. Open the scene with GameBootstrap and re-run.");
            }
        }

        private static void ConfigureTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void SaveMat(Material mat, string name)
        {
            string path = $"{OutputFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"MaterialCreator: Saved {path}");
        }
    }
}
