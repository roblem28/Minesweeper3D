using UnityEngine;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Adjusts a RectTransform to fit within Screen.safeArea, handling
    /// camera notches, punch holes, and rounded corners on mobile devices.
    /// Attach to the root child of a ScreenSpaceOverlay Canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            // Re-check if screen size or safe area changes (orientation change, etc.)
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x ||
                Screen.height != _lastScreenSize.y)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Convert safe area from screen coords to anchor coords (0-1)
            Vector2 anchorMin = new Vector2(
                safeArea.x / Screen.width,
                safeArea.y / Screen.height
            );
            Vector2 anchorMax = new Vector2(
                (safeArea.x + safeArea.width) / Screen.width,
                (safeArea.y + safeArea.height) / Screen.height
            );

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;

            Debug.Log($"[SafeArea] Applied: min={anchorMin}, max={anchorMax}, safeArea={safeArea}");
        }
    }
}
