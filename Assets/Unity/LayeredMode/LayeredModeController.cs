using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Minesweeper3D.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Minesweeper3D.Unity.LayeredMode
{
    /// <summary>
    /// Main orchestrator for LayeredMode. 5x5x3 grid displayed one Z layer
    /// at a time. Orthographic camera, no rotation. Column highlighting on tap.
    /// Uses existing Core engine (Board, Generator).
    /// </summary>
    public class LayeredModeController : MonoBehaviour
    {
        [Header("Materials (assign in inspector or auto-create)")]
        [SerializeField] private Material opaqueMaterial;

        public Board Board { get; private set; }
        public int GridWidth => 5;
        public int GridHeight => 5;
        public int GridDepth => 3;
        public int MineCount => 10;

        public Material OpaqueMaterial => opaqueMaterial;

        private int _currentLayer;
        private int _seed;
        private bool _firstClick = true;

        // Cell grid: [x, y, z]
        private LayeredCellView[,,] _cells;

        // Column highlight state
        private int _highlightX = -1;
        private int _highlightY = -1;

        // HUD
        private LayeredHUD _hud;

        // Timer
        private TimerController _timer;

        // Flag mode
        private bool _flagMode;
        public bool IsFlagMode => _flagMode;
        public void ToggleFlagMode() => _flagMode = !_flagMode;

        // Pending tap for double-tap chord
        private float _pendingTapTime;
        private Coord3? _pendingTapCoord;
        private const float DoubleTapWindow = 0.25f;

        // Touch state
        private float _touchStartTime;
        private Vector2 _touchStartPos;
        private bool _touchMoved;
        private const float TapMoveTolerance = 15f;
        private const float LongPressDuration = 0.4f;
        private bool _longPressHandled;

        public TimerController Timer => _timer;
        public int CurrentLayer => _currentLayer;
        public int HintsUsed { get; set; }

        private Camera _cam;

        private const float CellSpacing = 1.1f;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            _seed = Environment.TickCount;
            _currentLayer = 0;

            SetupCamera();
            SetupMaterial();
            BuildGrid();

            // Timer
            var timerObj = new GameObject("TimerController");
            _timer = timerObj.AddComponent<TimerController>();

            // HUD
            var hudObj = new GameObject("LayeredHUD");
            _hud = hudObj.AddComponent<LayeredHUD>();
            _hud.Init(this);

            RefreshVisuals();
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogError("[LayeredMode] No MainCamera found!");
                return;
            }

            _cam.orthographic = true;
            _cam.orthographicSize = 4f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.10f, 0.10f, 0.18f, 1f);

            // Position camera looking straight down at the grid (top-down view)
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.transform.rotation = Quaternion.identity;
        }

        private void SetupMaterial()
        {
            // Material will be set from the default cube primitive material.
            // If inspector material is assigned, use it; otherwise cells use
            // the built-in default material from CreatePrimitive.
            if (opaqueMaterial != null)
                LayeredCellView.SetSharedMaterial(opaqueMaterial);
        }

        private void BuildGrid()
        {
            _cells = new LayeredCellView[GridWidth, GridHeight, GridDepth];

            float offsetX = (GridWidth - 1) * CellSpacing * 0.5f;
            float offsetY = (GridHeight - 1) * CellSpacing * 0.5f;

            for (int z = 0; z < GridDepth; z++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = $"LCell_{x}_{y}_{z}";
                        go.transform.SetParent(transform);
                        go.transform.localPosition = new Vector3(
                            x * CellSpacing - offsetX,
                            y * CellSpacing - offsetY,
                            0f
                        );
                        go.transform.localScale = new Vector3(0.95f, 0.95f, 0.1f);

                        var cell = go.AddComponent<LayeredCellView>();
                        cell.Init(x, y, z);
                        _cells[x, y, z] = cell;
                    }
                }
            }

            ShowLayer(_currentLayer);
        }

        public void SetLayer(int layer)
        {
            layer = Mathf.Clamp(layer, 0, GridDepth - 1);
            if (layer == _currentLayer) return;
            _currentLayer = layer;
            ShowLayer(layer);
            RefreshVisuals();
            _hud?.Refresh();
        }

        private void ShowLayer(int z)
        {
            for (int dz = 0; dz < GridDepth; dz++)
            {
                bool active = (dz == z);
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        _cells[x, y, dz].gameObject.SetActive(active);
                    }
                }
            }
        }

        private void Update()
        {
            // Flush pending tap if double-tap window expired
            if (_pendingTapCoord.HasValue && Time.time - _pendingTapTime >= DoubleTapWindow)
            {
                HandleCellAction(_pendingTapCoord.Value);
                _pendingTapCoord = null;
            }

            if (Touch.activeTouches.Count > 0)
                HandleTouch();
            else
                HandleMouse();
        }

        // ===== Mouse Input =====

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Left click
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverUI()) return;
                Vector2 pos = mouse.position.ReadValue();
                if (TryRaycastCell(pos, out Coord3 coord))
                {
                    // Double click detection
                    if (_pendingTapCoord.HasValue && _pendingTapCoord.Value.Equals(coord))
                    {
                        _pendingTapCoord = null;
                        HandleChord(coord);
                    }
                    else
                    {
                        if (_pendingTapCoord.HasValue)
                            HandleCellAction(_pendingTapCoord.Value);
                        _pendingTapCoord = coord;
                        _pendingTapTime = Time.time;
                    }
                }

                // Column highlight
                UpdateColumnHighlight(pos);
            }

            // Right click = flag
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (IsPointerOverUI()) return;
                Vector2 pos = mouse.position.ReadValue();
                if (TryRaycastCell(pos, out Coord3 coord))
                    HandleFlag(coord);
            }

            // Hover: update column highlight
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (!IsPointerOverUI())
                    UpdateColumnHighlight(pos);
                else
                    ClearColumnHighlight();
            }
        }

        // ===== Touch Input =====

        private void HandleTouch()
        {
            if (Touch.activeTouches.Count != 1) return;
            var touch = Touch.activeTouches[0];

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchStartTime = Time.time;
                    _touchStartPos = touch.screenPosition;
                    _touchMoved = false;
                    _longPressHandled = false;
                    UpdateColumnHighlight(touch.screenPosition);
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    if (Vector2.Distance(touch.screenPosition, _touchStartPos) > TapMoveTolerance)
                        _touchMoved = true;

                    // Long press = flag
                    if (!_touchMoved && !_longPressHandled &&
                        Time.time - _touchStartTime >= LongPressDuration)
                    {
                        _longPressHandled = true;
                        if (!IsPointerOverUI() && TryRaycastCell(_touchStartPos, out Coord3 coord))
                            HandleFlag(coord);
                    }
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                    if (!_touchMoved && !_longPressHandled && !IsPointerOverUI())
                    {
                        if (TryRaycastCell(touch.screenPosition, out Coord3 coord))
                        {
                            // Double tap detection
                            if (_pendingTapCoord.HasValue && _pendingTapCoord.Value.Equals(coord))
                            {
                                _pendingTapCoord = null;
                                HandleChord(coord);
                            }
                            else
                            {
                                if (_pendingTapCoord.HasValue)
                                    HandleCellAction(_pendingTapCoord.Value);
                                _pendingTapCoord = coord;
                                _pendingTapTime = Time.time;
                            }
                        }
                    }
                    ClearColumnHighlight();
                    break;
            }
        }

        // ===== Game Actions =====

        private void HandleCellAction(Coord3 coord)
        {
            if (_flagMode)
                HandleFlag(coord);
            else
                HandleReveal(coord);
        }

        private void HandleReveal(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;

            if (_firstClick)
            {
                _firstClick = false;
                Board = GenerateLayeredBoard(coord);
                Board.Reveal(coord);

                // Reveal all out-of-play cells (Z >= GridDepth or x/y outside bounds)
                // so they don't block win condition or flood-fill
                RevealOutOfPlayCells();

                _timer.StartTimer();
                RefreshVisuals();
                _hud?.Refresh();
                return;
            }

            var rv = Board.Reveal(coord);
            if (rv == RevealResult.Ok || rv == RevealResult.Mine)
            {
                RefreshVisuals();
                _hud?.Refresh();
            }
        }

        private Board GenerateLayeredBoard(Coord3 firstClick)
        {
            int size = Mathf.Max(GridWidth, Mathf.Max(GridHeight, GridDepth));

            // Build exclusion set (firstClick + 26 neighbors)
            var excluded = new System.Collections.Generic.HashSet<int>();
            int fcIdx = firstClick.X + size * (firstClick.Y + size * firstClick.Z);
            excluded.Add(fcIdx);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                int nx = firstClick.X + dx;
                int ny = firstClick.Y + dy;
                int nz = firstClick.Z + dz;
                if (nx >= 0 && nx < size && ny >= 0 && ny < size && nz >= 0 && nz < size)
                    excluded.Add(nx + size * (ny + size * nz));
            }

            // Only place mines in playable area (x < GridWidth, y < GridHeight, z < GridDepth)
            var candidates = new System.Collections.Generic.List<int>();
            for (int z = 0; z < GridDepth; z++)
            for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
            {
                int idx = x + size * (y + size * z);
                if (!excluded.Contains(idx))
                    candidates.Add(idx);
            }

            var rng = new System.Random(_seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int mineCount = Mathf.Min(MineCount, candidates.Count);
            var mineCoords = new System.Collections.Generic.List<Coord3>(mineCount);
            for (int i = 0; i < mineCount; i++)
            {
                int idx = candidates[i];
                int mx = idx % size;
                int my = (idx / size) % size;
                int mz = idx / (size * size);
                mineCoords.Add(new Coord3(mx, my, mz));
            }

            return new Board(size, mineCoords);
        }

        private void RevealOutOfPlayCells()
        {
            // Reveal all cells outside the playable area so they don't affect
            // win condition or create invisible unrevealed pockets
            int size = Board.Size;
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (x >= GridWidth || y >= GridHeight || z >= GridDepth)
                    Board.Reveal(new Coord3(x, y, z));
            }
        }

        private void HandleFlag(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;
            if (Board.ToggleFlag(coord))
            {
                RefreshVisuals();
                _hud?.Refresh();
            }
        }

        private void HandleChord(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;
            if (Board.ChordReveal(coord))
            {
                RefreshVisuals();
                _hud?.Refresh();
            }
        }

        // ===== Column Highlighting =====

        private void UpdateColumnHighlight(Vector2 screenPos)
        {
            if (TryRaycastCell(screenPos, out Coord3 coord))
            {
                if (coord.X == _highlightX && coord.Y == _highlightY)
                    return;

                ClearColumnHighlight();
                _highlightX = coord.X;
                _highlightY = coord.Y;

                for (int z = 0; z < GridDepth; z++)
                    _cells[_highlightX, _highlightY, z].SetColumnHighlight(true);
            }
            else
            {
                ClearColumnHighlight();
            }
        }

        private void ClearColumnHighlight()
        {
            if (_highlightX < 0) return;
            for (int z = 0; z < GridDepth; z++)
                _cells[_highlightX, _highlightY, z].SetColumnHighlight(false);
            _highlightX = -1;
            _highlightY = -1;
        }

        // ===== Visuals =====

        public void RefreshVisuals()
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                _timer.StopTimer();

            bool gameOver = Board != null && Board.Status != GameStatus.Playing;

            for (int z = 0; z < GridDepth; z++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        var cell = _cells[x, y, z];
                        if (Board != null)
                        {
                            var coord = new Coord3(x, y, z);
                            cell.UpdateVisual(
                                Board.GetState(coord),
                                Board.GetCount(coord),
                                Board.IsMine(coord),
                                gameOver
                            );
                        }
                        else
                        {
                            cell.UpdateVisual(CellState.Hidden, 0, false, false);
                        }
                    }
                }
            }
        }

        public void RestartGame()
        {
            Board = null;
            _firstClick = true;
            _seed = Environment.TickCount;
            _currentLayer = 0;
            _flagMode = false;
            _pendingTapCoord = null;
            HintsUsed = 0;
            _timer.ResetTimer();
            ShowLayer(0);
            RefreshVisuals();
            _hud?.Refresh();
        }

        // ===== Helpers =====

        private bool TryRaycastCell(Vector2 screenPos, out Coord3 coord)
        {
            coord = default;
            if (_cam == null) return false;

            Ray ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var cell = hit.collider.GetComponent<LayeredCellView>();
                if (cell != null)
                {
                    coord = cell.Coord;
                    return true;
                }
            }
            return false;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // Win check helper: count only playable cells (Z < GridDepth)
        public int CountPlayableSafeCells()
        {
            if (Board == null) return 0;
            int count = 0;
            for (int z = 0; z < GridDepth; z++)
            for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
            {
                var c = new Coord3(x, y, z);
                if (!Board.IsMine(c)) count++;
            }
            return count;
        }
    }
}
