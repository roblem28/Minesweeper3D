using Minesweeper3D.Core;
using TMPro;
using UnityEngine;

namespace Minesweeper3D
{
    /// <summary>
    /// Example Unity-side binding showing HUD counter updates without using Update().
    /// Wire your click input system to SingleClickReveal / DoubleClickChord / ToggleFlag.
    /// </summary>
    public class GameHUDController : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TMP_Text hudText; // Optional single label (supports multi-line block)
        [SerializeField] private TMP_Text hiddenText;
        [SerializeField] private TMP_Text minesText;
        [SerializeField] private TMP_Text safeLeftText;

        private Board _board;
        private int _currentSlice = 1;
        private int _totalSlices = 1;

        [Header("OnGUI Fallback")]
        [SerializeField] private bool drawOnGUI = true;
        [SerializeField] private Rect onGuiRect = new Rect(12, 12, 320, 80);

        public void CreateBoard(int size, int mineCount, Coord3 firstClick, int seed)
        {
            _board = Generator.Generate(size, mineCount, firstClick, seed);
            _totalSlices = size;
            _currentSlice = size;
            UpdateHUD(); // required: after board creation
        }

        public void SetSlice(int currentSlice, int totalSlices)
        {
            _currentSlice = Mathf.Max(1, currentSlice);
            _totalSlices = Mathf.Max(1, totalSlices);
            UpdateHUD(); // required: after slice changes
        }

        public RevealResult SingleClickReveal(Coord3 coord)
        {
            if (_board == null) return RevealResult.OutOfBounds;

            var result = _board.Reveal(coord);
            UpdateHUD(); // required: after reveal
            return result;
        }

        public RevealResult DoubleClickChord(Coord3 coord)
        {
            if (_board == null) return RevealResult.OutOfBounds;

            var result = _board.ChordReveal(coord);
            UpdateHUD(); // required: after reveal/chord
            return result;
        }

        public bool ToggleFlag(Coord3 coord)
        {
            if (_board == null) return false;

            bool toggled = _board.ToggleFlag(coord);

            // Optional on flag toggles. We still refresh so any HUD that also tracks
            // flags or visual state stays synchronized.
            if (toggled)
                UpdateHUD();

            return toggled;
        }

        public void UpdateHUD()
        {
            if (_board == null) return;

            string line1 = $"Slice {_currentSlice}/{_totalSlices} — Mines: {_board.TotalMines()}";
            string line2 = $"Hidden: {_board.HiddenCount}";
            string line3 = $"Safe Left: {_board.SafeLeft}";
            string fullHud = BuildOnGuiHudText();

            if (hudText != null)
                hudText.text = fullHud;

            if (minesText != null)
                minesText.text = line1;

            if (hiddenText != null)
                hiddenText.text = line2;

            if (safeLeftText != null)
                safeLeftText.text = line3;
        }

        private string BuildOnGuiHudText()
        {
            int totalCells = _board.TotalCells;
            int totalMines = _board.TotalMines();
            int totalSafe = totalCells - totalMines;
            int hiddenCount = totalCells - _board.RevealedTotalCount;
            int safeLeft = totalSafe - _board.RevealedSafeCount;

            return
                $"Slice {_currentSlice}/{_totalSlices} — Mines: {totalMines}\n" +
                $"Hidden: {hiddenCount}\n" +
                $"Safe Left: {safeLeft}";
        }

        private void OnGUI()
        {
            if (!drawOnGUI || _board == null) return;

            GUI.Label(onGuiRect, BuildOnGuiHudText());
        }
    }
}
