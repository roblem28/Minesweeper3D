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

        // Hover/press state
        private bool _isHovered;
        private bool _isPressed;
        private float _currentScale;
        private float _targetScale;
        private Color _baseColor;
        private Coroutine _revealFadeCoroutine;

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

        // --- Count label colors (8-tier, high contrast on #D4D4D4 revealed bg) ---
        private static readonly Color[] CountColors =
        {
            Color.clear,                                    // 0 (never shown)
            new Color(0.05f, 0.10f, 0.75f),                 // 1 deep blue
            new Color(0.05f, 0.45f, 0.05f),                 // 2 forest green
            new Color(0.78f, 0.05f, 0.05f),                 // 3 strong red
            new Color(0.03f, 0.03f, 0.40f),                 // 4 navy
            new Color(0.45f, 0.00f, 0.10f),                 // 5 dark maroon
            new Color(0.00f, 0.40f, 0.40f),                 // 6 dark teal
            new Color(0.02f, 0.02f, 0.02f),                 // 7 near-black
            new Color(0.35f, 0.35f, 0.35f),                 // 8 dark gray
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
            _labelShadow.color = new Color(0f, 0f, 0f, 0.45f);

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
                _baseColor = ActiveMineRevealed;
                ApplyColor(ActiveMineRevealed);
                SetScale(CubeScale);
                _targetScale = CubeScale;
                SetLabelText("\u25CF"); // ● solid circle
                _label.color = new Color(0.08f, 0.08f, 0.08f);
                PlayMinePopIfNeeded();
                _prevState = state;
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    _baseColor = ActiveHidden;
                    ApplyColor(_isHovered ? _baseColor * 1.2f : _baseColor);
                    SetScale(CubeScale);
                    _targetScale = _isHovered ? CubeScale * 1.02f : CubeScale;
                    break;

                case CellState.Flagged:
                    _baseColor = ActiveFlagged;
                    ApplyColor(ActiveFlagged);
                    SetScale(CubeScale);
                    _targetScale = CubeScale;
                    SetLabelText("F");
                    _label.color = Color.white;
                    break;

                case CellState.Revealed:
                    // Skip if staggered animation is playing
                    if (_revealFadeCoroutine != null) break;

                    if (count == 0 && !isMine)
                    {
                        _renderer.enabled = false;
                        _label.gameObject.SetActive(false);
                        _labelShadow.gameObject.SetActive(false);
                    }
                    else
                    {
                        _baseColor = ActiveRevealed;
                        ApplyColor(ActiveRevealed);
                        SetScale(RevealedScale);
                        _targetScale = RevealedScale;
                        SetLabelText(count.ToString());
                        int ci = Mathf.Min(count, CountColors.Length - 1);
                        _label.color = CountColors[ci];
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

        // ----- Hover / Press feedback -----

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            UpdateInteractionScale();
            UpdateInteractionColor();
        }

        public void SetPressed(bool pressed)
        {
            _isPressed = pressed;
            UpdateInteractionScale();
        }

        private void UpdateInteractionScale()
        {
            if (!_isActive) return;
            float baseScale = (_prevState == CellState.Revealed) ? RevealedScale : CubeScale;
            if (_isPressed)
                _targetScale = baseScale * 0.95f;
            else if (_isHovered)
                _targetScale = baseScale * 1.02f;
            else
                _targetScale = baseScale;
        }

        private void UpdateInteractionColor()
        {
            if (!_isActive || _prevState == CellState.Revealed) return;
            if (_isHovered)
            {
                // Brighten by ~20%
                Color bright = _baseColor * 1.2f;
                bright.a = 1f;
                ApplyColor(bright);
            }
            else
            {
                ApplyColor(_baseColor);
            }
        }

        private void Update()
        {
            if (!_isActive) return;
            // Smooth scale interpolation (0.1s ease)
            float current = transform.localScale.x;
            if (Mathf.Abs(current - _targetScale) > 0.001f)
            {
                float speed = 1f / 0.1f; // 0.1s ease
                float next = Mathf.MoveTowards(current, _targetScale, speed * Time.deltaTime * Mathf.Abs(_targetScale - current) + speed * Time.deltaTime * 0.1f);
                // Exponential ease for smoother feel
                next = Mathf.Lerp(current, _targetScale, 1f - Mathf.Exp(-15f * Time.deltaTime));
                transform.localScale = Vector3.one * next;
            }
        }

        // ----- Staggered reveal animation -----

        /// <summary>Animate this cell's reveal with a delay for cascade effect.</summary>
        public void PlayRevealAnimation(float delay, int count)
        {
            if (_revealFadeCoroutine != null) StopCoroutine(_revealFadeCoroutine);
            _revealFadeCoroutine = StartCoroutine(RevealFadeRoutine(delay, count));
        }

        private IEnumerator RevealFadeRoutine(float delay, int count)
        {
            // Hide initially during delay
            if (count == 0)
            {
                _renderer.enabled = false;
                _label.gameObject.SetActive(false);
                _labelShadow.gameObject.SetActive(false);
            }
            else
            {
                // For numbered cells: start with hidden appearance, then animate
                ApplyColor(ActiveHidden);
                SetScale(CubeScale);
                _label.gameObject.SetActive(false);
                _labelShadow.gameObject.SetActive(false);
            }

            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (count == 0)
            {
                // Fade out: briefly show then disappear
                _renderer.enabled = true;
                float duration = 0.15f;
                float elapsed = 0f;
                Color startColor = ActiveHidden;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float alpha = 1f - t;
                    Color c = startColor;
                    c.a = alpha;
                    ApplyColor(c);
                    float s = Mathf.Lerp(CubeScale, CubeScale * 0.8f, t);
                    transform.localScale = Vector3.one * s;
                    yield return null;
                }
                _renderer.enabled = false;
                _label.gameObject.SetActive(false);
                _labelShadow.gameObject.SetActive(false);
            }
            else
            {
                // Numbered cell: fade color transition + number scale-in
                float colorDuration = 0.15f;
                float elapsed = 0f;
                while (elapsed < colorDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / colorDuration;
                    Color c = Color.Lerp(ActiveHidden, ActiveRevealed, t);
                    ApplyColor(c);
                    float s = Mathf.Lerp(CubeScale, RevealedScale, t);
                    transform.localScale = Vector3.one * s;
                    yield return null;
                }
                ApplyColor(ActiveRevealed);
                SetScale(RevealedScale);
                _targetScale = RevealedScale;

                // Number fade-in with scale 0.8 → 1.0
                _label.gameObject.SetActive(true);
                _labelShadow.gameObject.SetActive(true);
                SetLabelText(count.ToString());
                int ci = Mathf.Min(count, CountColors.Length - 1);
                Color numColor = CountColors[ci];

                float numDuration = 0.2f;
                elapsed = 0f;
                Vector3 labelBase = _label.transform.localPosition;
                Vector3 shadowBase = _labelShadow.transform.localPosition;
                while (elapsed < numDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / numDuration;
                    float eased = 1f - (1f - t) * (1f - t); // ease-out
                    _label.color = new Color(numColor.r, numColor.g, numColor.b, eased);
                    _labelShadow.color = new Color(0f, 0f, 0f, 0.35f * eased);
                    float labelScale = Mathf.Lerp(0.8f, 1f, eased);
                    _label.characterSize = 0.315f * labelScale;
                    _labelShadow.characterSize = 0.315f * labelScale;
                    yield return null;
                }
                _label.color = numColor;
                _labelShadow.color = new Color(0f, 0f, 0f, 0.35f);
                _label.characterSize = 0.315f;
                _labelShadow.characterSize = 0.315f;
            }

            _revealFadeCoroutine = null;
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

        // ----- Explosion VFX -----

        private static readonly string FragmentTag = "ExplosionFragment";
        private static Mesh _fragmentMesh;

        private static void EnsureFragmentMesh()
        {
            if (_fragmentMesh != null) return;
            // Procedural unit cube mesh — no CreatePrimitive, no collider dependencies
            _fragmentMesh = new Mesh { name = "Fragment" };
            var v = new Vector3[]
            {
                // Front
                new(-0.5f,-0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f), new( 0.5f,-0.5f,-0.5f),
                // Back
                new( 0.5f,-0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f,-0.5f, 0.5f),
                // Top
                new(-0.5f, 0.5f,-0.5f), new(-0.5f, 0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f), new( 0.5f, 0.5f,-0.5f),
                // Bottom
                new(-0.5f,-0.5f, 0.5f), new(-0.5f,-0.5f,-0.5f), new( 0.5f,-0.5f,-0.5f), new( 0.5f,-0.5f, 0.5f),
                // Left
                new(-0.5f,-0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f,-0.5f), new(-0.5f,-0.5f,-0.5f),
                // Right
                new( 0.5f,-0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f, 0.5f), new( 0.5f,-0.5f, 0.5f),
            };
            var n = new Vector3[]
            {
                Vector3.back,Vector3.back,Vector3.back,Vector3.back,
                Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward,
                Vector3.up,Vector3.up,Vector3.up,Vector3.up,
                Vector3.down,Vector3.down,Vector3.down,Vector3.down,
                Vector3.left,Vector3.left,Vector3.left,Vector3.left,
                Vector3.right,Vector3.right,Vector3.right,Vector3.right,
            };
            var t = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int b = f * 4; int i = f * 6;
                t[i]=b; t[i+1]=b+1; t[i+2]=b+2;
                t[i+3]=b; t[i+4]=b+2; t[i+5]=b+3;
            }
            _fragmentMesh.vertices = v;
            _fragmentMesh.normals = n;
            _fragmentMesh.triangles = t;
            _fragmentMesh.RecalculateBounds();
        }

        /// <summary>Flash this cell red for the given duration (Step 1).</summary>
        public void FlashRed(float duration)
        {
            StartCoroutine(FlashRedRoutine(duration));
        }

        private IEnumerator FlashRedRoutine(float duration)
        {
            ApplyColor(Color.red);
            yield return new WaitForSecondsRealtime(duration);
            // Restore — will be overwritten by charge or explode anyway
        }

        /// <summary>Glow white from center outward (Step 2).</summary>
        public void ChargeGlow(float duration)
        {
            StartCoroutine(ChargeGlowRoutine(duration));
        }

        private IEnumerator ChargeGlowRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Color c = Color.Lerp(Color.red, Color.white, t);
                ApplyColor(c);
                float s = Mathf.Lerp(CubeScale, CubeScale * 1.15f, t);
                transform.localScale = Vector3.one * s;
                yield return null;
            }
            ApplyColor(Color.white);
        }

        /// <summary>
        /// Fracture this cube into 8 sub-pieces that fly outward with physics.
        /// Uses fully procedural mesh — no CreatePrimitive.
        /// </summary>
        public void Explode()
        {
            Explode(Vector3.zero, 0f);
        }

        /// <summary>Explode with directional impulse from an epicenter.</summary>
        public void Explode(Vector3 epicenter, float blastForce)
        {
            EnsureFragmentMesh();
            _renderer.enabled = false;
            _label.gameObject.SetActive(false);
            _labelShadow.gameObject.SetActive(false);

            _renderer.GetPropertyBlock(_propBlock);
            Color fragColor = _propBlock.GetColor("_BaseColor");

            float half = transform.localScale.x * 0.5f;
            float fragScale = half;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var frag = new GameObject(FragmentTag);
                frag.transform.position = transform.position
                    + new Vector3(x, y, z) * fragScale * 0.25f;
                frag.transform.localScale = Vector3.one * fragScale;

                var mf = frag.AddComponent<MeshFilter>();
                mf.sharedMesh = _fragmentMesh;
                var rend = frag.AddComponent<MeshRenderer>();
                rend.sharedMaterial = _opaqueMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var pb = new MaterialPropertyBlock();
                pb.SetColor("_BaseColor", fragColor);
                pb.SetColor("_Color", fragColor);
                rend.SetPropertyBlock(pb);

                var rb = frag.AddComponent<Rigidbody>();
                rb.useGravity = false;
                Vector3 dir = new Vector3(x, y, z).normalized;
                Vector3 baseVel = dir * Random.Range(3f, 7f) + Random.insideUnitSphere * 2f;
                // Add directional blast if epicenter provided
                if (blastForce > 0f)
                {
                    Vector3 away = (frag.transform.position - epicenter).normalized;
                    baseVel += away * blastForce;
                }
                rb.linearVelocity = baseVel;
                rb.angularVelocity = Random.insideUnitSphere * Random.Range(5f, 15f);

                StartCoroutine(FadeAndDestroyFragment(rend, frag, 1.5f));
            }
        }

        private IEnumerator FadeAndDestroyFragment(Renderer rend, GameObject obj, float lifetime)
        {
            var pb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(pb);
            Color baseColor = pb.GetColor("_BaseColor");
            float startScale = obj.transform.localScale.x;
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                float alpha = 1f - t * t;
                Color c = baseColor;
                c.a = alpha;
                pb.SetColor("_BaseColor", c);
                pb.SetColor("_Color", c);
                rend.SetPropertyBlock(pb);
                obj.transform.localScale = Vector3.one * Mathf.Lerp(startScale, 0f, t * t);
                yield return null;
            }
            Destroy(obj);
        }

        /// <summary>Clean up any leftover explosion fragments (call on board reset).</summary>
        public static void CleanupFragments()
        {
            foreach (var obj in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (obj != null && obj.name == FragmentTag)
                    Destroy(obj.gameObject);
            }
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
