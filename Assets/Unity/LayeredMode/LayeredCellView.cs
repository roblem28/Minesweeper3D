using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity.LayeredMode
{
    /// <summary>
    /// Simple 2D flat cell for LayeredMode. Renders as a flat quad with a text label.
    /// Uses MaterialPropertyBlock for per-cell coloring, same URP pattern as CellView.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LayeredCellView : MonoBehaviour
    {
        private int _x, _y, _z;
        private MeshFilter _meshFilter;
        private MeshRenderer _renderer;
        private TextMesh _label;
        private Collider _collider;
        private MaterialPropertyBlock _propBlock;

        // Shared material — set once from inspector-assigned asset
        private static Material _sharedMat;

        private const float CellSize = 0.9f;

        // --- Colors ---
        private static readonly Color ColorHidden   = new Color(0.44f, 0.44f, 0.44f, 1f);
        private static readonly Color ColorRevealed  = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color ColorFlagged   = new Color(1.00f, 0.20f, 0.20f, 1f);
        private static readonly Color ColorMine      = new Color(0.10f, 0.10f, 0.10f, 1f);
        private static readonly Color ColorEmpty     = new Color(0.20f, 0.20f, 0.25f, 1f);

        // Column highlight
        private static readonly Color ColorColumnHighlight = new Color(0.4f, 0.7f, 1f, 0.4f);

        // Count label colors (same 5-tier as CellView)
        private static readonly Color[] CountColors =
        {
            Color.clear,                            // 0 (never shown)
            new Color(0.20f, 0.40f, 0.95f),         // 1 blue
            new Color(0.15f, 0.70f, 0.15f),         // 2 green
            new Color(0.90f, 0.15f, 0.15f),         // 3 red
            new Color(0.45f, 0.15f, 0.65f),         // 4 dark purple
            new Color(0.95f, 0.55f, 0.10f),         // 5+ orange
        };

        public Coord3 Coord => new Coord3(_x, _y, _z);

        private bool _isColumnHighlighted;
        private Color _baseColor;

        public static void SetSharedMaterial(Material mat)
        {
            _sharedMat = mat;
        }

        public void Init(int x, int y, int z)
        {
            _x = x;
            _y = y;
            _z = z;
            _meshFilter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            _propBlock = new MaterialPropertyBlock();

            // Material is already set by CreatePrimitive; override with shared if available
            if (_sharedMat != null)
                _renderer.sharedMaterial = _sharedMat;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            // Label child — positioned in front of the cube face.
            // Parent scale is (0.95, 0.95, 0.1), so we counter-scale Z
            // and offset to sit just in front of the -Z face.
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 0f, -6f);
            labelObj.transform.localScale = new Vector3(1f, 1f, 10f); // counter Z squish
            _label = labelObj.AddComponent<TextMesh>();
            _label.alignment = TextAlignment.Center;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.characterSize = 0.30f;
            _label.fontSize = 64;
            _label.fontStyle = FontStyle.Bold;
            _label.text = "";

            _baseColor = ColorHidden;
            ApplyColor(ColorHidden);
        }

        public void UpdateVisual(CellState state, int count, bool isMine, bool gameOver)
        {
            _renderer.enabled = true;
            _label.text = "";

            // Game over: expose unflagged mines
            if (gameOver && isMine && state != CellState.Flagged)
            {
                _baseColor = ColorMine;
                _label.text = "X";
                _label.color = new Color(0.9f, 0.15f, 0.15f);
                ApplyEffectiveColor();
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    _baseColor = ColorHidden;
                    break;

                case CellState.Flagged:
                    _baseColor = ColorFlagged;
                    _label.text = "F";
                    _label.color = Color.white;
                    break;

                case CellState.Revealed:
                    if (count == 0 && !isMine)
                    {
                        _baseColor = ColorEmpty;
                    }
                    else
                    {
                        _baseColor = ColorRevealed;
                        _label.text = count.ToString();
                        int ci = Mathf.Min(count, CountColors.Length - 1);
                        _label.color = CountColors[ci];
                    }
                    break;
            }

            ApplyEffectiveColor();
        }

        public void SetColumnHighlight(bool highlighted)
        {
            _isColumnHighlighted = highlighted;
            ApplyEffectiveColor();
        }

        private void ApplyEffectiveColor()
        {
            if (_isColumnHighlighted)
            {
                // Blend column highlight onto base color
                Color c = Color.Lerp(_baseColor, ColorColumnHighlight, 0.4f);
                ApplyColor(c);
            }
            else
            {
                ApplyColor(_baseColor);
            }
        }

        private void ApplyColor(Color c)
        {
            _propBlock.SetColor("_BaseColor", c); // URP
            _propBlock.SetColor("_Color", c);     // fallback
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
