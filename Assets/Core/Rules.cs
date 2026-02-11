using System.Collections.Generic;
using System.Linq;

namespace Minesweeper3D.Core
{
    /// <summary>
    /// Classic Minesweeper deduction rules for 3D boards.
    /// Each rule examines a revealed numbered cell and infers MINE or SAFE for hidden neighbors.
    /// </summary>
    public static class Rules
    {
        /// <summary>
        /// R1: All-hidden-are-mines.
        /// If a revealed cell's count == number of hidden (non-flagged) neighbors,
        /// all those hidden neighbors are mines.
        /// </summary>
        public static List<DeductionStep> AllHiddenAreMines(Board board, Coord3 cell)
        {
            var steps = new List<DeductionStep>();
            if (board.GetState(cell) != CellState.Revealed) return steps;

            int count = board.GetCount(cell);
            if (count == 0) return steps;

            var neighbors = board.GetNeighbors(cell);
            var flagged = neighbors.Where(n => board.GetState(n) == CellState.Flagged).ToList();
            var hidden = neighbors.Where(n => board.GetState(n) == CellState.Hidden).ToList();

            int remainingMines = count - flagged.Count;
            if (remainingMines > 0 && remainingMines == hidden.Count)
            {
                steps.Add(new DeductionStep
                {
                    RuleId = "R1",
                    SourceCells = new[] { cell },
                    AffectedCells = hidden.ToArray(),
                    InferredMine = true
                });
            }

            return steps;
        }

        /// <summary>
        /// R2: All-remaining-are-safe.
        /// If a revealed cell's count == number of flagged neighbors,
        /// all other hidden neighbors are safe.
        /// </summary>
        public static List<DeductionStep> AllRemainingAreSafe(Board board, Coord3 cell)
        {
            var steps = new List<DeductionStep>();
            if (board.GetState(cell) != CellState.Revealed) return steps;

            int count = board.GetCount(cell);
            var neighbors = board.GetNeighbors(cell);
            var flagged = neighbors.Where(n => board.GetState(n) == CellState.Flagged).ToList();
            var hidden = neighbors.Where(n => board.GetState(n) == CellState.Hidden).ToList();

            if (flagged.Count == count && hidden.Count > 0)
            {
                steps.Add(new DeductionStep
                {
                    RuleId = "R2",
                    SourceCells = new[] { cell },
                    AffectedCells = hidden.ToArray(),
                    InferredMine = false
                });
            }

            return steps;
        }
    }
}
