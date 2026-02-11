using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Renders a single cell as a cube with visual states: hidden, revealed, flagged, mine.
    /// Delegates click input to GameController.
    /// </summary>
    [RequireComponent(typeof(Renderer), typeof(Collider))]
    public class CellView : MonoBehaviour
    {
        private int _x, _y;
        private SliceController _slice;
        private GameController _game;
        private Renderer _renderer;
        private TextMesh _label;

        private static readonly Color HiddenColor = new Color(0.55f, 0.55f, 0.6f);
        private static readonly Color RevealedColor = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color FlaggedColor = new Color(0.9f, 0.35f, 0.1f);
        private static readonly Color MineColor = new Color(0.85f, 0.1f, 0.1f);

        private static readonly Color[] CountColors =
        {
            Color.clear,                     // 0 (no text)
            new Color(0.1f, 0.1f, 0.9f),    // 1 blue
            new Color(0.0f, 0.6f, 0.0f),    // 2 green
            new Color(0.9f, 0.0f, 0.0f),    // 3 red
            new Color(0.1f, 0.1f, 0.55f),   // 4 dark blue
            new Color(0.55f, 0.0f, 0.0f),   // 5 maroon
            new Color(0.0f, 0.55f, 0.55f),  // 6 teal
            Color.black,                     // 7
            new Color(0.5f, 0.5f, 0.5f),    // 8+
        };

        public Coord3 Coord => new Coord3(_x, _y, _slice.CurrentSlice);

        public void Init(int x, int y, SliceController slice, GameController game)
        {
            _x = x;
            _y = y;
            _slice = slice;
            _game = game;
            _renderer = GetComponent<Renderer>();

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = Vector3.zero;
            _label = labelObj.AddComponent<TextMesh>();
            _label.alignment = TextAlignment.Center;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.characterSize = 0.35f;
            _label.fontSize = 64;
            _label.fontStyle = FontStyle.Bold;
            _label.text = "";

            _renderer.material.color = HiddenColor;
        }

        public void UpdateVisual(CellState state, int count, bool isMine, bool gameOver)
        {
            // On game over, reveal unflagged mines
            if (gameOver && isMine && state != CellState.Flagged)
            {
                _renderer.material.color = MineColor;
                _label.text = "*";
                _label.color = Color.white;
                return;
            }

            switch (state)
            {
                case CellState.Hidden:
                    _renderer.material.color = HiddenColor;
                    _label.text = "";
                    break;

                case CellState.Flagged:
                    _renderer.material.color = FlaggedColor;
                    _label.text = "F";
                    _label.color = Color.white;
                    break;

                case CellState.Revealed:
                    if (isMine)
                    {
                        _renderer.material.color = MineColor;
                        _label.text = "*";
                        _label.color = Color.white;
                    }
                    else
                    {
                        _renderer.material.color = RevealedColor;
                        if (count > 0)
                        {
                            _label.text = count.ToString();
                            int ci = Mathf.Min(count, CountColors.Length - 1);
                            _label.color = CountColors[ci];
                        }
                        else
                        {
                            _label.text = "";
                        }
                    }
                    break;
            }
        }

        private void LateUpdate()
        {
            // Billboard: keep label facing the camera
            if (_label != null && Camera.main != null)
                _label.transform.rotation = Camera.main.transform.rotation;
        }

        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(0))
                _game.OnCellLeftClick(Coord);
            else if (Input.GetMouseButtonDown(1))
                _game.OnCellRightClick(Coord);
        }
    }
}
