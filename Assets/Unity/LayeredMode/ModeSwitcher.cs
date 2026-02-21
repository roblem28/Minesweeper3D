using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Minesweeper3D.Unity.LayeredMode
{
    /// <summary>
    /// Adds a "Layered Mode" / "3D Mode" toggle button to any scene.
    /// Attach to a GameObject in SampleScene to enable switching.
    /// Auto-detects which scene is active and shows the opposite option.
    /// </summary>
    public class ModeSwitcher : MonoBehaviour
    {
        private void Start()
        {
            // Create a small floating button in the bottom-right corner
            var canvasObj = new GameObject("ModeSwitcherCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // Above other HUDs
            canvasObj.AddComponent<GraphicRaycaster>();

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            string currentScene = SceneManager.GetActiveScene().name;
            bool isLayered = currentScene.Contains("Layered");
            string targetScene = isLayered ? "SampleScene" : "LayeredModeScene";
            string buttonLabel = isLayered ? "3D Mode" : "Layered";

            var btnObj = new GameObject("Btn_ModeSwitch");
            btnObj.transform.SetParent(canvasObj.transform, false);
            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(150f, 55f);
            rt.anchoredPosition = new Vector2(-10f, 60f);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.7f, 0.9f);
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(targetScene);
            });

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = buttonLabel;
            tmp.fontSize = 22;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
    }
}
