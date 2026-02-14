using UnityEngine;
using UnityEngine.InputSystem;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Main game orchestrator. Creates a board via Generator on first click,
    /// wires input (left click = reveal, right click = flag), manages game state.
    /// All game logic delegated to Core API.
    /// Raycasts only hit the active slice (ghost colliders are disabled).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int gridSize = 6;
        [SerializeField] private int mineCount = 10;
        [SerializeField] private int seed = -1; // -1 = random

        public Board Board { get; private set; }

        private SliceController _sliceController;
        private CameraController _cameraController;
        private bool _firstClick = true;

        private void Start()
        {
            if (seed < 0)
                seed = System.Environment.TickCount;

            var sliceObj = new GameObject("SliceController");
            _sliceController = sliceObj.AddComponent<SliceController>();
            _sliceController.Init(gridSize, this);

            var cam = Camera.main;
            if (cam != null)
            {
                _cameraController = cam.gameObject.AddComponent<CameraController>();
                float camDistance = gridSize * 2.5f;
                _cameraController.Init(Vector3.zero, camDistance);
            }
            else
            {
                Debug.LogError("[MineSweeper3D] No MainCamera found!");
            }

            Debug.Log($"[MineSweeper3D] Started — {gridSize}^3 grid, {mineCount} mines, slice {_sliceController.CurrentSlice + 1}/{gridSize}");
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool left  = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            if (!left && !right) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 pos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var cell = hit.collider.GetComponent<CellView>();
                if (cell != null)
                {
                    Coord3 coord = cell.Coord;
                    if (left)
                    {
                        Debug.Log($"[MineSweeper3D] L-click {coord}");
                        HandleReveal(coord);
                    }
                    else
                    {
                        Debug.Log($"[MineSweeper3D] R-click {coord}");
                        HandleFlag(coord);
                    }
                }
            }
        }

        private void HandleReveal(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;

            if (_firstClick)
            {
                _firstClick = false;
                Board = Generator.Generate(gridSize, mineCount, coord, seed);
                var result = Board.Reveal(coord);
                Debug.Log($"[MineSweeper3D] First click {coord} → {result}, status={Board.Status}");
                _sliceController.RefreshAll();
                return;
            }

            var rv = Board.Reveal(coord);
            Debug.Log($"[MineSweeper3D] Reveal {coord} → {rv}, status={Board.Status}");
            if (rv == RevealResult.Ok || rv == RevealResult.Mine)
                _sliceController.RefreshAll();
        }

        private void HandleFlag(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;

            bool toggled = Board.ToggleFlag(coord);
            Debug.Log($"[MineSweeper3D] Flag {coord} → toggled={toggled}");
            if (toggled)
                _sliceController.RefreshAll();
        }

        public void RestartGame()
        {
            Board = null;
            _firstClick = true;
            seed = System.Environment.TickCount;
            _sliceController.RefreshAll();
            Debug.Log("[MineSweeper3D] Restarted");
        }

        private void OnGUI()
        {
            int slice = _sliceController != null ? _sliceController.CurrentSlice + 1 : 0;
            int totalSlices = _sliceController != null ? _sliceController.Size : 0;

            var big = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            if (Board == null)
            {
                GUI.Label(new Rect(10, 10, 400, 30), $"Slice {slice}/{totalSlices}  —  Click a cell to start", big);
                GUI.Label(new Rect(10, 38, 450, 20), "Scroll = slice  |  Ctrl+Scroll = zoom  |  Middle-drag = orbit", small);
                return;
            }

            string status;
            if (Board.Status == GameStatus.Won)
                status = "YOU WIN!";
            else if (Board.Status == GameStatus.Lost)
                status = "GAME OVER";
            else
                status = "Playing";

            int safeLeft = Board.TotalSafe - Board.RevealedSafeCount;

            GUI.Label(new Rect(10, 10, 500, 30),
                $"Slice {slice}/{totalSlices}  —  {status}", big);
            GUI.Label(new Rect(10, 38, 500, 20),
                $"Mines: {Board.MineCount}   Flags: {Board.FlagCount}   Safe Left: {safeLeft}", small);
            GUI.Label(new Rect(10, 56, 450, 20),
                "Scroll = slice  |  Ctrl+Scroll = zoom  |  Middle-drag = orbit", small);

            if (Board.Status != GameStatus.Playing)
            {
                if (GUI.Button(new Rect(10, 80, 100, 28), "Restart"))
                    RestartGame();
            }
        }
    }
}
