using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Main game orchestrator. Creates a board via Generator on first click,
    /// wires input (left click = reveal, right click = flag), manages game state.
    /// All game logic delegated to Core API.
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

            // Create slice controller
            var sliceObj = new GameObject("SliceController");
            _sliceController = sliceObj.AddComponent<SliceController>();
            _sliceController.Init(gridSize, this);

            // Set up camera
            var cam = Camera.main;
            if (cam != null)
            {
                _cameraController = cam.gameObject.AddComponent<CameraController>();
                float camDistance = gridSize * 1.5f;
                _cameraController.Init(Vector3.zero, camDistance);
            }
        }

        public void OnCellLeftClick(Coord3 coord)
        {
            if (Board != null && Board.Status != GameStatus.Playing)
                return;

            if (_firstClick)
            {
                _firstClick = false;
                Board = Generator.Generate(gridSize, mineCount, coord, seed);
                Board.Reveal(coord);
                _sliceController.RefreshAll();
                return;
            }

            var result = Board.Reveal(coord);
            if (result == RevealResult.Ok || result == RevealResult.Mine)
                _sliceController.RefreshAll();
        }

        public void OnCellRightClick(Coord3 coord)
        {
            if (Board == null || Board.Status != GameStatus.Playing)
                return;

            if (Board.ToggleFlag(coord))
                _sliceController.RefreshAll();
        }

        public void RestartGame()
        {
            Board = null;
            _firstClick = true;
            seed = System.Environment.TickCount;
            _sliceController.RefreshAll();
        }

        private void OnGUI()
        {
            int slice = _sliceController != null ? _sliceController.CurrentSlice : 0;

            // Status bar
            string status;
            if (Board == null)
                status = "Click to start";
            else if (Board.Status == GameStatus.Won)
                status = "YOU WIN!";
            else if (Board.Status == GameStatus.Lost)
                status = "GAME OVER";
            else
                status = $"Mines: {mineCount}";

            GUI.Label(
                new Rect(10, 10, 300, 25),
                $"Slice: {slice + 1}/{gridSize}  |  {status}",
                new GUIStyle(GUI.skin.label) { fontSize = 16 }
            );

            GUI.Label(
                new Rect(10, 35, 400, 20),
                "Scroll=slice | Ctrl+Scroll=zoom | Middle-drag=orbit"
            );

            // Restart button on game over
            if (Board != null && Board.Status != GameStatus.Playing)
            {
                if (GUI.Button(new Rect(10, 60, 120, 30), "Restart"))
                    RestartGame();
            }
        }
    }
}
