using System.Collections;
using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Renders a single cell. Active slice: opaque cube with label on top face.
    /// Ghost slice: wireframe outline (mobile-compatible, no geometry shader).
    /// Uses inspector-assigned URP materials via GameController. Zero Shader.Find.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class CellView : MonoBehaviour
    {
        private int _x, _y, _z;
        private MeshFilter _meshFilter;
        private MeshRenderer _renderer;
        private TextMesh _label;
        private BoxCollider _collider;
        private MaterialPropertyBlock _propBlock;
        private bool _isActive;
        private Coroutine _hintCoroutine;

        // Shared meshes (created once)
        private static Mesh _cubeMesh;
        private static Mesh _wireframeMesh;

        // Shared materials — set once from inspector-assigned assets, never created at runtime
        private static Material _opaqueMat;
        private static Material _ghostMat;

        private const float CubeScale = 0.6f;
        private const float RevealedScale = 0.52f;

        // --- Active slice colors ---
        private static readonly Color ActiveHidden   = new Color(0.44f, 0.44f, 0.44f, 1f);
        private static readonly Color ActiveRevealed = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color ActiveFlagged  = new Color(1.00f, 0.20f, 0.20f, 1f);
        private static readonly Color ActiveMine     = new Color(0.10f, 0.10f, 0.10f, 1f);

        // --- Ghost wire colors ---
        private static readonly Color GhostHidden  = new Color(0.25f, 0.25f, 0.25f, 0.10f);
        private static readonly Color GhostRevealed = new Color(0.31f, 0.31f, 0.31f, 0.15f);
        private static readonly Color GhostFlagged = new Color(1.00f, 0.20f, 0.20f, 0.15f);
        private static readonly Color GhostMine    = new Color(0.10f, 0.10f, 0.10f, 0.12f);

        // --- Count label colors (5-tier) ---
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

        /// <summary>
        /// Provide the inspector-assigned materials once. Called before any Init().
        /// </summary>
        public static void SetSharedMaterials(Material opaque, Material ghost)
        {
            _opaqueMat = opaque;
            _ghostMat = ghost;
        }

        public void Init(int x, int y, int z)
        {
            _x = x;
            _y = y;
            _z = z;
            _meshFilter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _renderer.receiveShadows = true;
            _collider = GetComponent<BoxCollider>();
            _propBlock = new MaterialPropertyBlock();

            EnsureSharedMeshes();

            // Label child — positioned just above top face
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = new Vector3(0f, 0.42f, 0f);
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

        private static void EnsureSharedMeshes()
        {
            if (_cubeMesh != null) return;

            // Cache the cube mesh
            _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (_cubeMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(temp);
            }

            // Build wireframe mesh (procedural geometry, no shader needed)
            _wireframeMesh = WireframeMeshBuilder.Build(1f, 0.02f);

            Debug.Log($"[CellView] Opaque mat: {(_opaqueMat != null ? _opaqueMat.shader.name : "NULL")}, Ghost mat: {(_ghostMat != null ? _ghostMat.shader.name : "NULL")}");
        }

        /// <summary>Switch between opaque cube (active) and wireframe (ghost) rendering.</summary>
        public void SetActiveSlice(bool active)
        {
            _isActive = active;
            _collider.enabled = active;

            if (active)
            {
                _meshFilter.sharedMesh = _cubeMesh;
                _renderer.sharedMaterial = _opaqueMat;
            }
            else
            {
                _meshFilter.sharedMesh = _wireframeMesh;
                _renderer.sharedMaterial = _ghostMat;
            }
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
                SetScale(CubeScale);
                _label.text = "X";
                _label.color = new Color(0.9f, 0.15f, 0.15f);
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    ApplyColor(ActiveHidden);
                    SetScale(CubeScale);
                    break;

                case CellState.Flagged:
                    ApplyColor(ActiveFlagged);
                    SetScale(CubeScale);
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
                        SetScale(RevealedScale);
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
            SetScale(CubeScale);

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

        // ----- Transition support -----

        /// <summary>
        /// Set interpolated alpha during slice transition. t=1 is fully active, t≈0.08 is ghost.
        /// Mesh swap happens at t=0.5 threshold.
        /// </summary>
        public void SetTransitionAlpha(float alpha)
        {
            bool shouldBeCube = alpha > 0.5f;
            var currentMesh = _meshFilter.sharedMesh;

            if (shouldBeCube && currentMesh != _cubeMesh)
            {
                _meshFilter.sharedMesh = _cubeMesh;
                _renderer.sharedMaterial = _opaqueMat;
            }
            else if (!shouldBeCube && currentMesh != _wireframeMesh)
            {
                _meshFilter.sharedMesh = _wireframeMesh;
                _renderer.sharedMaterial = _ghostMat;
            }

            // Apply interpolated color
            Color c = _isActive ? ActiveHidden : GhostHidden;
            c.a = alpha;
            ApplyColor(c);
        }

        // ----- Hint highlight -----

        private static readonly Color HintColor = new Color(1f, 0.9f, 0.2f, 1f);

        public void HighlightHint()
        {
            if (_hintCoroutine != null) StopCoroutine(_hintCoroutine);
            _hintCoroutine = StartCoroutine(HintFlash());
        }

        private IEnumerator HintFlash()
        {
            _renderer.GetPropertyBlock(_propBlock);
            Color original = _propBlock.GetColor("_BaseColor");
            ApplyColor(HintColor);
            yield return new WaitForSeconds(3f);
            ApplyColor(original);
            _hintCoroutine = null;
        }

        // ----- Helpers -----

        private void ApplyColor(Color c)
        {
            _propBlock.SetColor("_BaseColor", c); // URP
            _propBlock.SetColor("_Color", c);     // fallback
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void SetScale(float s)
        {
            transform.localScale = Vector3.one * s;
        }

        private void LateUpdate()
        {
            // Billboard label toward camera (active cells with visible text only)
            if (_isActive && _label != null && _label.text.Length > 0 && Camera.main != null)
                _label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
