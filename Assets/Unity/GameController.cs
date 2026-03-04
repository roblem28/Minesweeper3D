using System.Collections;
using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Main game orchestrator. Creates board on first click, manages game state.
    /// All game logic delegated to Core API. Input comes from InputManager events.
    /// </summary>
    public enum Difficulty { Easy, Medium, Hard }

    public class GameController : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int gridSize = 4;
        [SerializeField] private int mineCount = 6;
        [SerializeField] private int seed = -1; // -1 = random

        [Header("Materials (assign URP materials in inspector)")]
        [SerializeField] private Material opaqueMaterial;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Material floorMaterial;

        public Board Board { get; private set; }
        public int GridSize => gridSize;
        public int MineCount => mineCount;
        public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
        public bool IsCustomGame { get; private set; }

        private InputManager _inputManager;
        private SliceController _sliceController;
        private CameraController _cameraController;
        private HUDController _hudController;
        private HighlightController _highlightController;
        private TimerController _timer;
        private FeedbackManager _feedback;
        private bool _firstClick = true;
        private bool _winSequencePlaying;

        public TimerController Timer => _timer;
        public SliceController Slice => _sliceController;
        public int HintsUsed { get; set; }

        // Expose materials for child objects
        public Material OpaqueMaterial => opaqueMaterial;
        public Material GhostMaterial => ghostMaterial;
        public Material FloorMaterial => floorMaterial;

        private void Start()
        {
            if (seed < 0)
                seed = System.Environment.TickCount;

            if (opaqueMaterial == null || ghostMaterial == null || floorMaterial == null)
                Debug.LogError("[MineSweeper3D] Materials not assigned! Run Tools > Create & Assign Materials.");

            // Default to Easy (4x4x4, 6 mines)
            CurrentDifficulty = Difficulty.Easy;
            gridSize = 4;
            mineCount = 6;

            // Slice controller — builds the NxNxN grid
            var sliceObj = new GameObject("SliceController");
            _sliceController = sliceObj.AddComponent<SliceController>();
            _sliceController.Init(gridSize, this);

            // Camera controller
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                CreateBackgroundGradient(cam);

                _cameraController = cam.gameObject.AddComponent<CameraController>();
                float gridWorldSize = gridSize * SliceController.Spacing;
                _cameraController.Init(Vector3.zero, gridWorldSize);
            }
            else
            {
                Debug.LogError("[MineSweeper3D] No MainCamera found!");
            }

            // Input manager — handles all PC and mobile input
            var inputObj = new GameObject("InputManager");
            _inputManager = inputObj.AddComponent<InputManager>();
            _inputManager.Init(cam);

            // Core game events
            _inputManager.OnReveal += HandleReveal;
            _inputManager.OnFlag += HandleFlag;
            _inputManager.OnSliceChange += HandleSliceChange;
            _inputManager.OnOrbit += delta => _cameraController?.ApplyOrbit(delta);
            _inputManager.OnZoom += delta => _cameraController?.ApplyZoom(delta);

            // Timer
            var timerObj = new GameObject("TimerController");
            _timer = timerObj.AddComponent<TimerController>();

            // HUD
            var hudObj = new GameObject("HUDCanvas");
            _hudController = hudObj.AddComponent<HUDController>();
            _hudController.Init(this, _sliceController, _inputManager);

            // Feedback manager (sound + haptics)
            var feedbackObj = new GameObject("FeedbackManager");
            _feedback = feedbackObj.AddComponent<FeedbackManager>();
            _feedback.Init();

            // Highlight controller
            var highlightObj = new GameObject("HighlightController");
            _highlightController = highlightObj.AddComponent<HighlightController>();
            _highlightController.Init(this, _sliceController, _hudController);

            // Highlight events (desktop hover + mobile long press)
            _inputManager.OnHoverEnter += HandleHoverEnter;
            _inputManager.OnHoverExit += HandleHoverExit;
            _inputManager.OnHighlightStart += HandleHighlightStart;
            _inputManager.OnHighlightEnd += HandleHighlightEnd;

            // Press feedback
            _inputManager.OnPressDown += HandlePressDown;
            _inputManager.OnPressUp += HandlePressUp;

            // Chord events (desktop double-click + mobile double-tap)
            _inputManager.OnDoubleClick += HandleChord;
            _inputManager.OnDoubleTap += HandleChord;

            // Pre-warm fragment pool (512 = 64 cells × 8 fragments each)
            CellView.WarmFragmentPool(512);

            Debug.Log($"[MineSweeper3D] Started — {gridSize}^3 grid, {mineCount} mines");
        }

        private void HandleReveal(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;
            if (_winSequencePlaying) return;

            // Flag mode redirect: when flag toggle is active, taps become flags
            if (_hudController != null && _hudController.IsFlagMode)
            {
                HandleFlag(coord);
                return;
            }

            if (_firstClick)
            {
                _firstClick = false;
                Board = Generator.Generate(gridSize, mineCount, coord, seed);
                mineCount = Board.MineCount; // sync in case generator boosted mines
                Board.Reveal(coord);
                _timer.StartTimer();
                _feedback?.PlayTap();
                _feedback?.VibrateLight();
                int cascadeCount = Board.LastRevealed.Count;
                if (cascadeCount > 1) _feedback?.PlayRevealCascade(cascadeCount);
                RefreshWithCascade();
                if (Board.Status == GameStatus.Won)
                {
                    StartCoroutine(WinCelebrationSequence());
                    return;
                }
                PlayEndFeedback();
                return;
            }

            var rv = Board.Reveal(coord);
            if (rv == RevealResult.Ok || rv == RevealResult.Mine)
            {
                _feedback?.PlayTap();
                if (rv == RevealResult.Ok)
                {
                    _feedback?.VibrateLight();
                    if (Board.LastRevealed.Count > 1)
                        _feedback?.PlayRevealCascade(Board.LastRevealed.Count);
                }
                if (rv == RevealResult.Mine)
                {
                    StartCoroutine(MineExplosionSequence(coord));
                    return; // sequence handles refresh + feedback internally
                }
                RefreshWithCascade();
                if (Board.Status == GameStatus.Won)
                {
                    StartCoroutine(WinCelebrationSequence());
                    return;
                }
                PlayEndFeedback();
            }
        }

        private void HandleFlag(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;
            if (_winSequencePlaying) return;

            if (Board.ToggleFlag(coord))
            {
                bool isFlagged = Board.GetState(coord) == CellState.Flagged;
                if (isFlagged)
                    _feedback?.PlayFlag();
                else
                    _feedback?.PlayUnflag();
                _feedback?.VibrateLight();
                RefreshUI();
                if (Board.Status == GameStatus.Won)
                {
                    StartCoroutine(WinCelebrationSequence());
                    return;
                }
                PlayEndFeedback();
            }
        }

        private void HandleSliceChange(int direction)
        {
            HandleSliceChangePublic(direction);
        }

        public void HandleSliceChangePublic(int direction)
        {
            _sliceController.SetSlice(_sliceController.CurrentSlice + direction);
            _hudController?.Refresh();
            _hudController?.FlashSliceIndicator();
        }

        // --- Hover/press cell feedback ---

        private CellView _hoveredCell;

        private void HandleHoverEnter(Coord3 coord)
        {
            _highlightController?.BeginHighlight(coord);
            if (_hoveredCell != null) _hoveredCell.SetHovered(false);
            _hoveredCell = _sliceController.GetCell(coord);
            _hoveredCell.SetHovered(true);
        }

        private void HandleHoverExit()
        {
            _highlightController?.EndHighlight();
            if (_hoveredCell != null) { _hoveredCell.SetHovered(false); _hoveredCell = null; }
        }

        private void HandleHighlightStart(Coord3 coord)
        {
            _highlightController?.BeginHighlight(coord);
        }

        private void HandleHighlightEnd()
        {
            _highlightController?.EndHighlight();
        }

        // --- Press feedback ---

        private CellView _pressedCell;

        private void HandlePressDown(Coord3 coord)
        {
            if (_pressedCell != null) _pressedCell.SetPressed(false);
            _pressedCell = _sliceController.GetCell(coord);
            _pressedCell.SetPressed(true);
        }

        private void HandlePressUp()
        {
            if (_pressedCell != null) { _pressedCell.SetPressed(false); _pressedCell = null; }
        }

        // --- Chord handler ---

        private void HandleChord(Coord3 coord)
        {
            _highlightController?.TryChord(coord);
        }

        // --- Public API for HighlightController ---

        public void TriggerRefreshUI() => RefreshUI();

        public void ApplyDifficulty(Difficulty diff)
        {
            CurrentDifficulty = diff;
            int size = diff switch
            {
                Difficulty.Easy   => 4,
                Difficulty.Medium => 5,
                Difficulty.Hard   => 6,
                _ => 5
            };
            float density = diff switch
            {
                Difficulty.Easy   => 0.18f,
                Difficulty.Medium => 0.20f,
                Difficulty.Hard   => 0.22f,
                _ => 0.20f
            };
            int mines = Mathf.Max(1, Mathf.RoundToInt(size * size * size * density));
            RestartWithSettings(size, mines);
            IsCustomGame = false; // override after RestartWithSettings sets it
        }

        public void RestartGame()
        {
            if (IsCustomGame)
                RestartWithSettings(gridSize, mineCount);
            else
                ApplyDifficulty(CurrentDifficulty);
        }

        public void RestartWithSettings(int newGridSize, int newMineCount)
        {
            IsCustomGame = true;
            gridSize = newGridSize;
            mineCount = newMineCount;

            // Clean up win sequence state
            _winSequencePlaying = false;
            StopAllCoroutines();

            // Reset grid position (may have been lifted by win sequence)
            if (_sliceController != null)
                _sliceController.transform.position = Vector3.zero;

            // Clean up explosion fragments
            CellView.CleanupFragments();

            // Destroy old grid immediately so no leftover cells remain
            if (_sliceController != null)
                DestroyImmediate(_sliceController.gameObject);

            // Rebuild
            var sliceObj = new GameObject("SliceController");
            _sliceController = sliceObj.AddComponent<SliceController>();
            _sliceController.Init(gridSize, this);

            // Update camera framing
            if (_cameraController != null)
            {
                float gridWorldSize = gridSize * SliceController.Spacing;
                _cameraController.Init(Vector3.zero, gridWorldSize);
            }

            Board = null;
            _firstClick = true;
            seed = System.Environment.TickCount;
            _timer.ResetTimer();
            HintsUsed = 0;

            _hudController?.Rebind(_sliceController);
            _highlightController?.Rebind(_sliceController);
            _highlightController?.ClearCache();
            RefreshUI();
        }

        // --- Mine explosion cinematic sequence ---

        private IEnumerator MineExplosionSequence(Coord3 coord)
        {
            var mineCell = _sliceController.GetCell(coord);
            Vector3 epicenter = mineCell.transform.position;

            // Step 1: FLASH — clicked mine flashes red (100ms)
            mineCell.FlashRed(0.1f);
            yield return new WaitForSecondsRealtime(0.1f);

            // Step 2: CHARGE — slow-motion, mine glows white, bass rumble
            Time.timeScale = 0.3f;
            _feedback?.PlayChargeRumble();
            mineCell.ChargeGlow(0.2f);
            yield return new WaitForSecondsRealtime(0.2f);

            // Step 3: EXPLOSION — snap back to real-time
            Time.timeScale = 1f;

            // Explode the mine cell itself
            _feedback?.PlayExplosion(epicenter);
            mineCell.Explode(epicenter, 5f);

            // Camera shake + pullback (50% further than original)
            _cameraController?.Shake(0.4f, 0.2f);
            _cameraController?.Pullback(3f, 0.4f);
            _feedback?.VibrateMedium();

            // Explode ALL cubes on the board, staggered by distance from mine
            int size = _sliceController.Size;
            float maxDist = 0f;

            // First pass: find max distance for delay normalization
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var cell = _sliceController.GetCell(new Coord3(x, y, z));
                if (cell == null || cell == mineCell) continue;
                float d = Vector3.Distance(epicenter, cell.transform.position);
                if (d > maxDist) maxDist = d;
            }

            // Build list of (cell, distance) sorted by distance for staggered detonation
            var cellsByDist = new System.Collections.Generic.List<(CellView cell, float dist)>();
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var cell = _sliceController.GetCell(new Coord3(x, y, z));
                if (cell == null || cell == mineCell) continue;
                float d = Vector3.Distance(epicenter, cell.transform.position);
                cellsByDist.Add((cell, d));
            }
            cellsByDist.Sort((a, b) => a.dist.CompareTo(b.dist));

            // Second pass: explode each cell with distance-based delay and force
            float prevDelay = 0f;
            foreach (var (cell, dist) in cellsByDist)
            {
                // Delay: 50ms adjacent → 300ms far, scaled by normalized distance
                float t = maxDist > 0f ? dist / maxDist : 0f;
                float delay = Mathf.Lerp(0.05f, 0.3f, t);

                // Wait the incremental time since the last detonation
                float wait = delay - prevDelay;
                if (wait > 0.001f)
                    yield return new WaitForSecondsRealtime(wait);
                prevDelay = delay;

                // Force: closer cubes fly harder (5→1), far cubes tumble gently
                float force = Mathf.Lerp(5f, 1f, t);
                cell.Explode(epicenter, force);
            }

            // Step 4: DISSOLVE — wait for fragments to fade, then show game over
            _timer.StopTimer();

            // Play lose sound
            _feedback?.PlayLose();

            // Wait for fragment dissolve (1.5s lifetime) before showing Game Over UI
            yield return new WaitForSecondsRealtime(1.5f);

            // Now show Game Over — HUD Refresh triggers ShowEndPanel
            _hudController?.Refresh();
        }

        // --- Win celebration cinematic sequence ---

        private IEnumerator WinCelebrationSequence()
        {
            _winSequencePlaying = true;

            // Step 1: LOCK — stop timer, auto-reveal remaining hidden cubes
            _timer.StopTimer();

            int size = _sliceController.Size;
            var hiddenCells = new System.Collections.Generic.List<(CellView cell, Coord3 coord)>();
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = new Coord3(x, y, z);
                if (Board.GetState(c) == CellState.Hidden && !Board.IsMine(c))
                {
                    var cell = _sliceController.GetCell(c);
                    if (cell != null)
                        hiddenCells.Add((cell, c));
                }
            }

            // Stagger auto-reveal at 20ms per cell
            float revealDelay = 0f;
            foreach (var (cell, coord) in hiddenCells)
            {
                int count = Board.GetCount(coord);
                cell.PlayRevealAnimation(revealDelay, count);
                revealDelay += 0.02f;
            }
            // Wait for all reveals to finish (stagger + animation time)
            float revealWait = Mathf.Max(0.4f, revealDelay + 0.2f);
            yield return new WaitForSecondsRealtime(revealWait);

            // Step 4: SOUND + HAPTIC (fires at ~0.4s, overlaps with glow start)
            _feedback?.PlayWinChime();
            _feedback?.VibrateWin();

            // Step 2: GLOW — emission wave traveling outward from last revealed cell
            Coord3 origin;
            var lastRevealed = Board.LastRevealed;
            if (lastRevealed != null && lastRevealed.Count > 0)
                origin = lastRevealed[lastRevealed.Count - 1];
            else
                origin = new Coord3(size / 2, size / 2, size / 2);

            Vector3 originPos = _sliceController.GetCell(origin).transform.position;

            var glowCells = new System.Collections.Generic.List<(CellView cell, float dist)>();
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var cell = _sliceController.GetCell(new Coord3(x, y, z));
                if (cell == null) continue;
                // Only glow visible cells (renderer enabled)
                if (!cell.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(originPos, cell.transform.position);
                glowCells.Add((cell, d));
            }
            glowCells.Sort((a, b) => a.dist.CompareTo(b.dist));

            float maxGlowDist = glowCells.Count > 0 ? glowCells[glowCells.Count - 1].dist : 1f;
            foreach (var (cell, dist) in glowCells)
            {
                float t = maxGlowDist > 0f ? dist / maxGlowDist : 0f;
                float delay = t * 0.4f; // spread over 0.4s
                cell.PulseGlow(delay);
            }

            // Step 3: LIFT — float up 0.3 units + scale pulse (overlaps glow)
            yield return new WaitForSecondsRealtime(0.2f); // slight delay into glow
            float liftDuration = 0.5f;
            float liftElapsed = 0f;
            Vector3 startPos = _sliceController.transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.3f;
            Vector3 startScale = _sliceController.transform.localScale;
            while (liftElapsed < liftDuration)
            {
                liftElapsed += Time.unscaledDeltaTime;
                float lt = Mathf.Clamp01(liftElapsed / liftDuration);
                // Ease-out for position
                float eased = 1f - (1f - lt) * (1f - lt);
                _sliceController.transform.position = Vector3.Lerp(startPos, endPos, eased);
                // Scale pulse: 1.0 → 1.03 → 1.0 (ease-in-out)
                float scalePulse = 1f + 0.03f * Mathf.Sin(lt * Mathf.PI);
                _sliceController.transform.localScale = startScale * scalePulse;
                yield return null;
            }
            _sliceController.transform.position = endPos;
            _sliceController.transform.localScale = startScale;

            // Step 5: UI — show win panel after sequence completes
            yield return new WaitForSecondsRealtime(0.3f);
            _hudController?.Refresh();
        }

        private void PlayEndFeedback()
        {
            if (Board == null) return;
            if (Board.Status == GameStatus.Won)
            {
                _feedback?.PlayWin();
                _feedback?.VibratePattern();
            }
            else if (Board.Status == GameStatus.Lost)
            {
                _feedback?.PlayLose();
            }
        }

        private void RefreshWithCascade()
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                _timer.StopTimer();

            var lastRevealed = Board?.LastRevealed;
            if (lastRevealed != null && lastRevealed.Count > 1)
            {
                // Refresh non-revealed cells immediately
                _sliceController.RefreshAll();

                // Stagger cascade: 25ms per cell, capped at 500ms total
                float perCell = 0.025f;
                float maxDelay = 0.5f;
                for (int i = 0; i < lastRevealed.Count; i++)
                {
                    var coord = lastRevealed[i];
                    var cell = _sliceController.GetCell(coord);
                    float delay = Mathf.Min(i * perCell, maxDelay);
                    int count = Board.GetCount(coord);
                    cell.PlayRevealAnimation(delay, count);
                }
            }
            else
            {
                _sliceController.RefreshAll();
            }

            _highlightController?.RefreshCrossSliceIndicators();
            _hudController?.Refresh();
        }

        private void RefreshUI()
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                _timer.StopTimer();

            _sliceController.RefreshAll();
            _highlightController?.RefreshCrossSliceIndicators();
            _hudController?.Refresh();
        }

        private void CreateBackgroundGradient(Camera cam)
        {
            // Create 1x256 gradient texture at runtime
            var tex = new Texture2D(1, 256, TextureFormat.RGB24, false);
            Color topColor = new Color(0.08f, 0.08f, 0.16f);     // dark navy
            Color bottomColor = new Color(0.14f, 0.12f, 0.22f);  // slightly purple
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                tex.SetPixel(0, i, Color.Lerp(bottomColor, topColor, t));
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BackgroundGradient";
            Destroy(go.GetComponent<Collider>());

            // Parent to camera so it always fills view
            go.transform.SetParent(cam.transform);
            float dist = cam.farClipPlane - 1f;
            go.transform.localPosition = new Vector3(0f, 0f, dist);
            float height = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * cam.aspect;
            go.transform.localScale = new Vector3(width, height, 1f);
            go.transform.localRotation = Quaternion.identity;

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Reuse ghost material's shader (URP Unlit) to avoid Shader.Find at runtime
            if (ghostMaterial != null)
            {
                var mat = new Material(ghostMaterial.shader);
                mat.SetFloat("_Surface", 0f); // opaque
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
                renderer.material = mat;
            }
        }

        private void OnDestroy()
        {
            if (_inputManager != null)
            {
                _inputManager.OnReveal -= HandleReveal;
                _inputManager.OnFlag -= HandleFlag;
                _inputManager.OnSliceChange -= HandleSliceChange;
                _inputManager.OnHoverEnter -= HandleHoverEnter;
                _inputManager.OnHoverExit -= HandleHoverExit;
                _inputManager.OnHighlightStart -= HandleHighlightStart;
                _inputManager.OnHighlightEnd -= HandleHighlightEnd;
                _inputManager.OnPressDown -= HandlePressDown;
                _inputManager.OnPressUp -= HandlePressUp;
                _inputManager.OnDoubleClick -= HandleChord;
                _inputManager.OnDoubleTap -= HandleChord;
            }
        }
    }
}
