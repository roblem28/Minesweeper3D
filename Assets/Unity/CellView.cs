using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Renders a single cell as a cube. Supports two modes:
    /// Active (opaque, interactive) and Ghost (transparent, non-interactive).
    /// Uses shared materials with per-cell MaterialPropertyBlock for colors.
    /// </summary>
    [RequireComponent(typeof(Renderer), typeof(BoxCollider))]
    public class CellView : MonoBehaviour
    {
        private int _x, _y, _z;
        private Renderer _renderer;
        private TextMesh _label;
        private BoxCollider _collider;
        private MaterialPropertyBlock _propBlock;
        private bool _isActive;

        // Two shared materials for all cells (created once)
        private static Material _opaqueMat;
        private static Material _ghostMat;

        // --- Active slice colors (fully opaque) ---
        private static readonly Color ActiveHidden  = new Color(0.50f, 0.50f, 0.55f, 1f);
        private static readonly Color ActiveRevealed = new Color(0.82f, 0.82f, 0.88f, 1f);
        private static readonly Color ActiveFlagged = new Color(0.90f, 0.30f, 0.10f, 1f);
        private static readonly Color ActiveMine    = new Color(0.10f, 0.10f, 0.10f, 1f);

        // --- Ghost slice colors (transparent) ---
        private const float GhostAlpha = 0.12f;
        private static readonly Color GhostHidden   = new Color(0.50f, 0.50f, 0.55f, GhostAlpha);
        private static readonly Color GhostRevealed = new Color(0.30f, 0.40f, 0.80f, GhostAlpha);
        private static readonly Color GhostFlagged  = new Color(0.80f, 0.20f, 0.10f, GhostAlpha);
        private static readonly Color GhostMine     = new Color(0.10f, 0.10f, 0.10f, GhostAlpha);

        // --- Count label colors ---
        private static readonly Color[] CountColors =
        {
            Color.clear,                     // 0 (never shown)
            new Color(0.15f, 0.15f, 0.95f),  // 1 blue
            new Color(0.05f, 0.65f, 0.05f),  // 2 green
            new Color(0.95f, 0.10f, 0.10f),  // 3 red
            new Color(0.10f, 0.10f, 0.55f),  // 4 dark blue
            new Color(0.55f, 0.05f, 0.05f),  // 5 maroon
            new Color(0.05f, 0.55f, 0.55f),  // 6 teal
            new Color(0.15f, 0.15f, 0.15f),  // 7 dark
            new Color(0.45f, 0.45f, 0.45f),  // 8+
        };

        public Coord3 Coord => new Coord3(_x, _y, _z);

        public void Init(int x, int y, int z)
        {
            _x = x;
            _y = y;
            _z = z;
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<BoxCollider>();
            _propBlock = new MaterialPropertyBlock();

            EnsureSharedMaterials(_renderer.sharedMaterial);

            // Label child for count numbers
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = Vector3.zero;
            _label = labelObj.AddComponent<TextMesh>();
            _label.alignment = TextAlignment.Center;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.characterSize = 0.35f;
            _label.fontSize = 64;
            _label.fontStyle = FontStyle.Bold;
            _label.text = "";

            // Start as ghost
            SetActiveSlice(false);
        }

        private static void EnsureSharedMaterials(Material source)
        {
            if (_opaqueMat != null) return;

            // Opaque — clone the default cube material as-is
            _opaqueMat = new Material(source);

            // Ghost — clone and switch to transparent blending
            _ghostMat = new Material(source);

            // URP Lit shader
            if (_ghostMat.HasProperty("_Surface"))
            {
                _ghostMat.SetFloat("_Surface", 1f);
                _ghostMat.SetFloat("_Blend", 0f);
                _ghostMat.SetOverrideTag("RenderType", "Transparent");
                _ghostMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _ghostMat.EnableKeyword("_ALPHABLEND_ON");
            }
            // Built-in Standard shader fallback
            if (_ghostMat.HasProperty("_Mode"))
            {
                _ghostMat.SetFloat("_Mode", 3f);
                _ghostMat.DisableKeyword("_ALPHATEST_ON");
                _ghostMat.EnableKeyword("_ALPHABLEND_ON");
                _ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            // Blend settings common to both pipelines
            _ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _ghostMat.SetInt("_ZWrite", 0);
            _ghostMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Switch between opaque (active) and transparent (ghost) rendering.</summary>
        public void SetActiveSlice(bool active)
        {
            _isActive = active;
            _collider.enabled = active;
            _renderer.sharedMaterial = active ? _opaqueMat : _ghostMat;
        }

        /// <summary>Update visual state from board data.</summary>
        public void UpdateVisual(CellState state, int count, bool isMine, bool gameOver)
        {
            if (_isActive)
                UpdateActive(state, count, isMine, gameOver);
            else
                UpdateGhost(state, count, isMine, gameOver);
        }

        // ----- Active slice rendering -----

        private void UpdateActive(CellState state, int count, bool isMine, bool gameOver)
        {
            _renderer.enabled = true;
            _label.gameObject.SetActive(true);
            _label.text = "";

            // Game over: expose unflagged mines
            if (gameOver && isMine && state != CellState.Flagged)
            {
                ApplyColor(ActiveMine);
                _label.text = "*";
                _label.color = Color.white;
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    ApplyColor(ActiveHidden);
                    break;

                case CellState.Flagged:
                    ApplyColor(ActiveFlagged);
                    _label.text = "F";
                    _label.color = Color.white;
                    break;

                case CellState.Revealed:
                    if (count == 0 && !isMine)
                    {
                        _renderer.enabled = false;
                        _label.gameObject.SetActive(false);
                    }
                    else
                    {
                        ApplyColor(ActiveRevealed);
                        _label.text = count.ToString();
                        int ci = Mathf.Min(count, CountColors.Length - 1);
                        _label.color = CountColors[ci];
                    }
                    break;
            }
        }

        // ----- Ghost slice rendering -----

        private void UpdateGhost(CellState state, int count, bool isMine, bool gameOver)
        {
            _label.gameObject.SetActive(false);

            // Game over: ghost mines
            if (gameOver && isMine && state != CellState.Flagged)
            {
                _renderer.enabled = true;
                ApplyColor(GhostMine);
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    _renderer.enabled = true;
                    ApplyColor(GhostHidden);
                    break;

                case CellState.Flagged:
                    _renderer.enabled = true;
                    ApplyColor(GhostFlagged);
                    break;

                case CellState.Revealed:
                    if (count == 0 && !isMine)
                    {
                        _renderer.enabled = false;
                    }
                    else
                    {
                        _renderer.enabled = true;
                        ApplyColor(GhostRevealed);
                    }
                    break;
            }
        }

        // ----- Helpers -----

        private void ApplyColor(Color c)
        {
            _propBlock.SetColor("_BaseColor", c); // URP
            _propBlock.SetColor("_Color", c);     // Standard fallback
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void LateUpdate()
        {
            // Billboard label toward camera (active cells with visible text only)
            if (_isActive && _label != null && _label.text.Length > 0 && Camera.main != null)
                _label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
