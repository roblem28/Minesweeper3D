using UnityEngine;
using UnityEngine.InputSystem;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Creates ALL NxNxN cells as a 3D grid. The active slice is opaque and
    /// interactive; other slices render as transparent ghosts.
    /// Scroll wheel changes the active slice.
    /// </summary>
    public class SliceController : MonoBehaviour
    {
        private int _size;
        private int _currentSlice;
        private GameController _game;
        private CellView[,,] _cells;

        private const float Spacing = 1.2f;
        private const float CubeScale = 0.8f;

        public int CurrentSlice => _currentSlice;
        public int Size => _size;

        public void Init(int size, GameController game)
        {
            _size = size;
            _game = game;
            _currentSlice = size / 2;
            _cells = new CellView[size, size, size];
            BuildGrid();
            RefreshAll();
        }

        private void BuildGrid()
        {
            float offset = (_size - 1) * Spacing * 0.5f;

            for (int z = 0; z < _size; z++)
            {
                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = $"Cell_{x}_{y}_{z}";
                        go.transform.SetParent(transform);
                        go.transform.localPosition = new Vector3(
                            x * Spacing - offset,
                            y * Spacing - offset,
                            z * Spacing - offset
                        );
                        go.transform.localScale = Vector3.one * CubeScale;

                        var cell = go.AddComponent<CellView>();
                        cell.Init(x, y, z);
                        _cells[x, y, z] = cell;
                    }
                }
            }
        }

        public void SetSlice(int z)
        {
            z = Mathf.Clamp(z, 0, _size - 1);
            if (z == _currentSlice) return;
            _currentSlice = z;
            Debug.Log($"[MineSweeper3D] Slice → {_currentSlice + 1}/{_size}");
            RefreshAll();
        }

        public void RefreshAll()
        {
            Board board = _game.Board;
            bool gameOver = board != null && board.Status != GameStatus.Playing;

            for (int z = 0; z < _size; z++)
            {
                bool active = (z == _currentSlice);

                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        var cell = _cells[x, y, z];
                        cell.SetActiveSlice(active);

                        if (board != null)
                        {
                            var coord = new Coord3(x, y, z);
                            cell.UpdateVisual(
                                board.GetState(coord),
                                board.GetCount(coord),
                                board.IsMine(coord),
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

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f) return;

            // Ctrl+scroll reserved for camera zoom
            var kb = Keyboard.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed))
                return;

            if (scrollY > 0f)
                SetSlice(_currentSlice + 1);
            else
                SetSlice(_currentSlice - 1);
        }
    }
}
