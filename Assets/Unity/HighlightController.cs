using System.Collections.Generic;
using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Centralized controller for neighbor highlighting, remaining-mine HUD,
    /// cross-slice indicators, and chord logic.
    /// </summary>
    public class HighlightController : MonoBehaviour
    {
        private GameController _game;
        private SliceController _slice;
        private HUDController _hud;

        // Current highlight state
        private Coord3? _highlightedCell;
        private readonly List<Coord3> _highlightedNeighbors = new List<Coord3>(26);
        private readonly Dictionary<Coord3, Color> _savedColors = new Dictionary<Coord3, Color>(27);

        // Neighbor cache: avoids per-frame GetNeighbors allocation
        private readonly Dictionary<Coord3, List<Coord3>> _neighborCache
            = new Dictionary<Coord3, List<Coord3>>(256);

        // Highlight colors — active slice
        private static readonly Color HighlightUnrevealed = new Color(1f, 0.95f, 0.4f, 1f);
        private static readonly Color HighlightFlagged    = new Color(1f, 0.15f, 0.15f, 1f);
        private static readonly Color HighlightNumbered   = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color HighlightCleared    = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // Highlight colors — ghost slice (lower alpha)
        private static readonly Color GhostHighlightUnrevealed = new Color(1f, 0.95f, 0.4f, 0.25f);
        private static readonly Color GhostHighlightFlagged    = new Color(1f, 0.15f, 0.15f, 0.25f);
        private static readonly Color GhostHighlightNumbered   = new Color(1f, 1f, 1f, 0.15f);

        // Source cell highlight
        private static readonly Color SourceHighlight = new Color(0.4f, 0.9f, 1f, 1f);

        public void Init(GameController game, SliceController slice, HUDController hud)
        {
            _game = game;
            _slice = slice;
            _hud = hud;
        }

        public void Rebind(SliceController newSlice)
        {
            EndHighlight();
            _slice = newSlice;
        }

        public void ClearCache()
        {
            _neighborCache.Clear();
        }

        // --- Highlight API ---

        public void BeginHighlight(Coord3 cell)
        {
            Board board = _game.Board;
            if (board == null || board.Status != GameStatus.Playing) return;

            // Clear previous highlight if any
            if (_highlightedCell.HasValue)
                EndHighlight();

            _highlightedCell = cell;

            // Get neighbors (cached)
            var neighbors = GetCachedNeighbors(board, cell);
            _highlightedNeighbors.Clear();
            _highlightedNeighbors.AddRange(neighbors);

            int currentSlice = _slice.CurrentSlice;
            bool hasAboveSlice = false;
            bool hasBelowSlice = false;

            // Save and highlight the source cell itself
            var sourceView = _slice.GetCell(cell);
            _savedColors[cell] = sourceView.GetCurrentColor();
            sourceView.ApplyHighlightColor(SourceHighlight);

            // Save and highlight each neighbor
            for (int i = 0; i < _highlightedNeighbors.Count; i++)
            {
                var n = _highlightedNeighbors[i];
                var cellView = _slice.GetCell(n);

                // Save original color
                _savedColors[n] = cellView.GetCurrentColor();

                // Determine highlight color based on state and slice
                bool isGhost = n.Z != currentSlice;
                Color highlightColor = GetHighlightColor(board, n, isGhost);
                cellView.ApplyHighlightColor(highlightColor);

                // Track cross-slice neighbors
                if (n.Z > currentSlice) hasAboveSlice = true;
                if (n.Z < currentSlice) hasBelowSlice = true;
            }

            // Pulse slice arrows if neighbors exist on other slices
            if (hasAboveSlice) _hud.PulseSliceArrow(1);
            if (hasBelowSlice) _hud.PulseSliceArrow(-1);

            // Feature 2: Show remaining mine count for numbered cells
            CellState cellState = board.GetState(cell);
            int count = board.GetCount(cell);
            if (cellState == CellState.Revealed && count > 0)
            {
                int flaggedNeighbors = board.CountFlaggedNeighbors(cell);
                int remaining = Mathf.Max(0, count - flaggedNeighbors);
                _hud.ShowRemainingInfo(count, remaining);
            }
            else if (cellState == CellState.Flagged)
            {
                // For flagged cells, still show neighbor info
                _hud.HideRemainingInfo();
            }
            else
            {
                _hud.HideRemainingInfo();
            }
        }

        public void EndHighlight()
        {
            if (!_highlightedCell.HasValue) return;

            // Restore all saved colors
            foreach (var kvp in _savedColors)
            {
                var cellView = _slice.GetCell(kvp.Key);
                cellView.ApplyHighlightColor(kvp.Value);
            }

            _savedColors.Clear();
            _highlightedNeighbors.Clear();
            _highlightedCell = null;
            _hud.HideRemainingInfo();
        }

        // --- Chord API ---

        public void TryChord(Coord3 cell)
        {
            Board board = _game.Board;
            if (board == null || board.Status != GameStatus.Playing) return;

            bool changed = board.ChordReveal(cell);
            if (changed)
                _game.TriggerRefreshUI();
        }

        // --- Cross-Slice Indicators (Feature 3) ---

        public void RefreshCrossSliceIndicators()
        {
            Board board = _game.Board;
            if (board == null) return;

            int size = board.Size;
            int z = _slice.CurrentSlice;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var coord = new Coord3(x, y, z);
                    var cellView = _slice.GetCell(coord);
                    CellState state = board.GetState(coord);
                    int count = board.GetCount(coord);

                    if (state == CellState.Revealed && count > 0)
                    {
                        board.GetCrossSliceStatus(coord, out bool hasAbove, out bool hasBelow);
                        if (hasAbove || hasBelow)
                            cellView.ShowCrossSliceMarker(hasAbove, hasBelow);
                        else
                            cellView.HideCrossSliceMarker();
                    }
                    else
                    {
                        cellView.HideCrossSliceMarker();
                    }
                }
            }
        }

        // --- Internal helpers ---

        private List<Coord3> GetCachedNeighbors(Board board, Coord3 cell)
        {
            if (_neighborCache.TryGetValue(cell, out var cached))
                return cached;

            var neighbors = board.GetNeighbors(cell);
            _neighborCache[cell] = neighbors;
            return neighbors;
        }

        private Color GetHighlightColor(Board board, Coord3 neighbor, bool isGhost)
        {
            CellState state = board.GetState(neighbor);

            switch (state)
            {
                case CellState.Hidden:
                    return isGhost ? GhostHighlightUnrevealed : HighlightUnrevealed;

                case CellState.Flagged:
                    return isGhost ? GhostHighlightFlagged : HighlightFlagged;

                case CellState.Revealed:
                    int count = board.GetCount(neighbor);
                    if (count > 0)
                        return isGhost ? GhostHighlightNumbered : HighlightNumbered;
                    return HighlightCleared;

                default:
                    return HighlightCleared;
            }
        }
    }
}
