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
        private TextMesh _labelShadow;
        private TextMesh _crossSliceMarker;
        private BoxCollider _collider;
        private MaterialPropertyBlock _propBlock;
        private bool _isActive;
        private Coroutine _hintCoroutine;

        // Animation state
        private CellState _prevState = CellState.Hidden;
        private Coroutine _revealPopCoroutine;
        private bool _minePopped;
        private Coroutine _minePopCoroutine;

        // Shared meshes (created once)
        private static Mesh _cubeMesh;
        private static Mesh _wireframeMesh;

        // Shared materials — set once from inspector-assigned assets, never created at runtime
        private static Material _opaqueMat;
        private static Material _ghostMat;

        private const float CubeScale = 0.6f;
        private const float RevealedScale = 0.52f;

        // --- Active slice colors ---
        private static readonly Color ActiveHidden       = new Color(0.949f, 0.949f, 0.949f, 1f);  // #F2F2F2 warm off-white
        private static readonly Color ActiveRevealed     = new Color(0.831f, 0.831f, 0.831f, 1f);  // #D4D4D4 darker neutral
        private static readonly Color ActiveFlagged      = new Color(1.00f, 0.20f, 0.20f, 1f);
        private static readonly Color ActiveMine         = new Color(0.10f, 0.10f, 0.10f, 1f);
        private static readonly Color ActiveMineRevealed = new Color(0.35f, 0.10f, 0.10f, 1f);     // dark red-tinted

        // --- Ghost wire colors (dim yellow wireframe) ---
        private static readonly Color GhostHidden  = new Color(0.67f, 0.67f, 0.27f, 0.15f);
        private static readonly Color GhostRevealed = new Color(0.67f, 0.67f, 0.27f, 0.10f);
        private static readonly Color GhostFlagged = new Color(0.80f, 0.20f, 0.20f, 0.15f);
        private static readonly Color GhostMine    = new Color(0.67f, 0.67f, 0.27f, 0.12f);

        // --- Count label colors (8-tier classic Minesweeper) ---
        private static readonly Color[] CountColors =
        {
            Color.clear,                                    // 0 (never shown)
            new Color(0.106f, 0.184f, 0.898f),              // 1 blue
            new Color(0.110f, 0.549f, 0.110f),              // 2 green
            new Color(0.898f, 0.125f, 0.125f),              // 3 red
            new Color(0.071f, 0.071f, 0.478f),              // 4 dark blue
            new Color(0.502f, 0.000f, 0.125f),              // 5 maroon
            new Color(0.000f, 0.502f, 0.502f),              // 6 teal
            new Color(0.05f,  0.05f,  0.05f),               // 7 black
            new Color(0.50f,  0.50f,  0.50f),               // 8 gray
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

            // Label shadow — slightly offset for drop shadow effect
            var shadowObj = new GameObject("LabelShadow");
            shadowObj.transform.SetParent(transform);
            shadowObj.transform.localPosition = new Vector3(0.012f, 0.41f, 0.012f);
            _labelShadow = shadowObj.AddComponent<TextMesh>();
            _labelShadow.alignment = TextAlignment.Center;
            _labelShadow.anchor = TextAnchor.MiddleCenter;
            _labelShadow.characterSize = 0.315f;
            _labelShadow.fontSize = 58;
            _labelShadow.fontStyle = FontStyle.Bold;
            _labelShadow.text = "";
            _labelShadow.color = new Color(0f, 0f, 0f, 0.35f);

            // Label child — positioned just above top face
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            _label = labelObj.AddComponent<TextMesh>();
            _label.alignment = TextAlignment.Center;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.characterSize = 0.315f;
            _label.fontSize = 58;
            _label.fontStyle = FontStyle.Bold;
            _label.text = "";

            // Cross-slice indicator child
            var markerObj = new GameObject("CrossSliceMarker");
            markerObj.transform.SetParent(transform);
            markerObj.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            _crossSliceMarker = markerObj.AddComponent<TextMesh>();
            _crossSliceMarker.alignment = TextAlignment.Center;
            _crossSliceMarker.anchor = TextAnchor.MiddleCenter;
            _crossSliceMarker.characterSize = 0.18f;
            _crossSliceMarker.fontSize = 48;
            _crossSliceMarker.fontStyle = FontStyle.Bold;
            _crossSliceMarker.text = "";
            _crossSliceMarker.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            markerObj.SetActive(false);

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
            _wireframeMesh = WireframeMeshBuilder.Build(1f, 0.018f);

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
            if (!gameOver) _minePopped = false;

            _renderer.enabled = true;
            _label.gameObject.SetActive(true);
            _labelShadow.gameObject.SetActive(true);
            SetLabelText("");

            // Game over: expose unflagged mines
            if (gameOver && isMine && state != CellState.Flagged)
            {
                ApplyColor(ActiveMineRevealed);
                SetScale(CubeScale);
                SetLabelText("\u25CF"); // ● solid circle
                _label.color = new Color(0.08f, 0.08f, 0.08f);
                PlayMinePopIfNeeded();
                _prevState = state;
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
                    SetLabelText("F");
                    _label.color = Color.white;
                    break;

                case CellState.Revealed:
                    if (count == 0 && !isMine)
                    {
                        _renderer.enabled = false;
                        _label.gameObject.SetActive(false);
                        _labelShadow.gameObject.SetActive(false);
                    }
                    else
                    {
                        ApplyColor(ActiveRevealed);
                        SetScale(RevealedScale);
                        SetLabelText(count.ToString());
                        int ci = Mathf.Min(count, CountColors.Length - 1);
                        _label.color = CountColors[ci];

                        // Trigger pop on fresh reveal
                        if (_prevState != CellState.Revealed)
                        {
                            if (_revealPopCoroutine != null) StopCoroutine(_revealPopCoroutine);
                            _revealPopCoroutine = StartCoroutine(RevealPopRoutine());
                        }
                    }
                    break;
            }

            _prevState = state;
        }

        // ----- Ghost slice rendering -----

        private void UpdateGhost(CellState state, int count, bool isMine, bool gameOver)
        {
            _label.gameObject.SetActive(false);
            _labelShadow.gameObject.SetActive(false);
            HideCrossSliceMarker();
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

        // ----- Animations -----

        private IEnumerator RevealPopRoutine()
        {
            float duration = 0.1f;
            float elapsed = 0f;
            float startScale = RevealedScale * 1.05f;
            transform.localScale = Vector3.one * startScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = 1f - (1f - t) * (1f - t); // ease-out quadratic
                float s = Mathf.Lerp(startScale, RevealedScale, eased);
                transform.localScale = Vector3.one * s;
                yield return null;
            }
            SetScale(RevealedScale);
            _revealPopCoroutine = null;
        }

        private void PlayMinePopIfNeeded()
        {
            if (_minePopped) return;
            _minePopped = true;
            if (_minePopCoroutine != null) StopCoroutine(_minePopCoroutine);
            _minePopCoroutine = StartCoroutine(MinePopRoutine());
        }

        private IEnumerator MinePopRoutine()
        {
            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = t < 0.5f
                    ? Mathf.Lerp(CubeScale, CubeScale * 1.1f, t * 2f)
                    : Mathf.Lerp(CubeScale * 1.1f, CubeScale, (t - 0.5f) * 2f);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            SetScale(CubeScale);
            _minePopCoroutine = null;
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

        // ----- Highlight support -----

        public Color GetCurrentColor()
        {
            _renderer.GetPropertyBlock(_propBlock);
            return _propBlock.GetColor("_BaseColor");
        }

        public void ApplyHighlightColor(Color c)
        {
            ApplyColor(c);
        }

        // ----- Cross-slice indicators -----

        public void ShowCrossSliceMarker(bool hasAbove, bool hasBelow)
        {
            if (!_isActive) { HideCrossSliceMarker(); return; }
            string marker = "";
            if (hasAbove && hasBelow) marker = "\u25B2\u25BC";
            else if (hasAbove) marker = "\u25B2";
            else if (hasBelow) marker = "\u25BC";

            if (marker.Length > 0)
            {
                _crossSliceMarker.text = marker;
                _crossSliceMarker.gameObject.SetActive(true);
            }
            else
            {
                _crossSliceMarker.gameObject.SetActive(false);
            }
        }

        public void HideCrossSliceMarker()
        {
            if (_crossSliceMarker != null)
                _crossSliceMarker.gameObject.SetActive(false);
        }

        // ----- Helpers -----

        private void SetLabelText(string text)
        {
            _label.text = text;
            _labelShadow.text = text;
        }

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
            if (!_isActive || Camera.main == null) return;
            var camRot = Camera.main.transform.rotation;
            // Billboard label toward camera (active cells with visible text only)
            if (_label != null && _label.text.Length > 0)
            {
                _label.transform.rotation = camRot;
                _labelShadow.transform.rotation = camRot;
            }
            // Billboard cross-slice marker
            if (_crossSliceMarker != null && _crossSliceMarker.gameObject.activeSelf)
                _crossSliceMarker.transform.rotation = camRot;
        }
    }
}
