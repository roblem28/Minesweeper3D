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
        [SerializeField] private int gridSize = 5;
        [SerializeField] private int mineCount = 18;
        [SerializeField] private int seed = -1; // -1 = random

        public Board Board { get; private set; }
        public int GridSize => gridSize;
        public int MineCount => mineCount;
        public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Medium;

        private InputManager _inputManager;
        private SliceController _sliceController;
        private CameraController _cameraController;
        private HUDController _hudController;
        private bool _firstClick = true;

        private void Start()
        {
            if (seed < 0)
                seed = System.Environment.TickCount;

            // Slice controller — builds the NxNxN grid
            var sliceObj = new GameObject("SliceController");
            _sliceController = sliceObj.AddComponent<SliceController>();
            _sliceController.Init(gridSize, this);

            // Camera controller
            var cam = Camera.main;
            if (cam != null)
            {
                // Dark background for contrast against cubes
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);

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

            _inputManager.OnReveal += HandleReveal;
            _inputManager.OnFlag += HandleFlag;
            _inputManager.OnSliceChange += HandleSliceChange;
            _inputManager.OnOrbit += delta => _cameraController?.ApplyOrbit(delta);
            _inputManager.OnZoom += delta => _cameraController?.ApplyZoom(delta);

            // HUD
            var hudObj = new GameObject("HUDCanvas");
            _hudController = hudObj.AddComponent<HUDController>();
            _hudController.Init(this, _sliceController, _inputManager);

            Debug.Log($"[MineSweeper3D] Started — {gridSize}^3 grid, {mineCount} mines");
        }

        private void HandleReveal(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;

            if (_firstClick)
            {
                _firstClick = false;
                Board = Generator.Generate(gridSize, mineCount, coord, seed);
                Board.Reveal(coord);
                RefreshUI();
                return;
            }

            var rv = Board.Reveal(coord);
            if (rv == RevealResult.Ok || rv == RevealResult.Mine)
                RefreshUI();
        }

        private void HandleFlag(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;

            if (Board.ToggleFlag(coord))
                RefreshUI();
        }

        private void HandleSliceChange(int direction)
        {
            _sliceController.SetSlice(_sliceController.CurrentSlice + direction);
            _hudController?.Refresh();
        }

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
                Difficulty.Easy   => 0.10f,
                Difficulty.Medium => 0.14f,
                Difficulty.Hard   => 0.17f,
                _ => 0.14f
            };
            int mines = Mathf.Max(1, Mathf.RoundToInt(size * size * size * density));
            RestartWithSettings(size, mines);
        }

        public void RestartGame()
        {
            ApplyDifficulty(CurrentDifficulty);
        }

        public void RestartWithSettings(int newGridSize, int newMineCount)
        {
            gridSize = newGridSize;
            mineCount = newMineCount;

            // Destroy old grid
            if (_sliceController != null)
                Destroy(_sliceController.gameObject);

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

            _hudController?.Rebind(_sliceController);
            RefreshUI();
        }

        private void RefreshUI()
        {
            _sliceController.RefreshAll();
            _hudController?.Refresh();
        }

        private void OnDestroy()
        {
            if (_inputManager != null)
            {
                _inputManager.OnReveal -= HandleReveal;
                _inputManager.OnFlag -= HandleFlag;
                _inputManager.OnSliceChange -= HandleSliceChange;
            }
        }
    }
}
