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

            Debug.Log($"[MineSweeper3D] Started — {gridSize}^3 grid, {mineCount} mines");
        }

        private void HandleReveal(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;

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
                int cascadeCount = Board.LastRevealed.Count;
                if (cascadeCount > 1) _feedback?.PlayRevealCascade(cascadeCount);
                RefreshWithCascade();
                PlayEndFeedback();
                return;
            }

            var rv = Board.Reveal(coord);
            if (rv == RevealResult.Ok || rv == RevealResult.Mine)
            {
                _feedback?.PlayTap();
                if (rv == RevealResult.Ok && Board.LastRevealed.Count > 1)
                    _feedback?.PlayRevealCascade(Board.LastRevealed.Count);
                if (rv == RevealResult.Mine)
                {
                    _feedback?.PlayMineReveal();
                    _feedback?.VibrateHeavy();
                }
                RefreshWithCascade();
                PlayEndFeedback();
            }
        }

        private void HandleFlag(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;

            if (Board.ToggleFlag(coord))
            {
                bool isFlagged = Board.GetState(coord) == CellState.Flagged;
                if (isFlagged)
                    _feedback?.PlayFlag();
                else
                    _feedback?.PlayUnflag();
                _feedback?.VibrateLight();
                RefreshUI();
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
