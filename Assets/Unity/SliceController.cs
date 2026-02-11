using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Displays one Z-slice of the 3D grid as an NxN grid of CellViews.
    /// Scroll wheel changes the current slice.
    /// </summary>
    public class SliceController : MonoBehaviour
    {
        private int _size;
        private int _currentSlice;
        private GameController _game;
        private CellView[,] _cells;

        private const float Spacing = 1.1f;

        public int CurrentSlice => _currentSlice;

        public void Init(int size, GameController game)
        {
            _size = size;
            _game = game;
            _currentSlice = size / 2; // start in the middle
            _cells = new CellView[size, size];
            BuildGrid();
        }

        private void BuildGrid()
        {
            float offset = (_size - 1) * Spacing * 0.5f;

            for (int x = 0; x < _size; x++)
            {
                for (int y = 0; y < _size; y++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Cell_{x}_{y}";
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(
                        x * Spacing - offset,
                        y * Spacing - offset,
                        0f
                    );
                    go.transform.localScale = Vector3.one * 0.95f;

                    var cell = go.AddComponent<CellView>();
                    cell.Init(x, y, this, _game);
                    _cells[x, y] = cell;
                }
            }
        }

        public void SetSlice(int z)
        {
            z = Mathf.Clamp(z, 0, _size - 1);
            if (z == _currentSlice) return;
            _currentSlice = z;
            RefreshAll();
        }

        public void RefreshAll()
        {
            Board board = _game.Board;
            bool gameOver = board != null && board.Status != GameStatus.Playing;

            for (int x = 0; x < _size; x++)
            {
                for (int y = 0; y < _size; y++)
                {
                    var coord = new Coord3(x, y, _currentSlice);
                    if (board != null)
                    {
                        _cells[x, y].UpdateVisual(
                            board.GetState(coord),
                            board.GetCount(coord),
                            board.IsMine(coord),
                            gameOver
                        );
                    }
                    else
                    {
                        _cells[x, y].UpdateVisual(CellState.Hidden, 0, false, false);
                    }
                }
            }
        }

        private void Update()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f) return;

            // Ctrl+scroll is reserved for camera zoom
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return;

            if (scroll > 0f)
                SetSlice(_currentSlice + 1);
            else
                SetSlice(_currentSlice - 1);
        }
    }
}
