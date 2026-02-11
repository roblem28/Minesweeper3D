using System.Collections.Generic;
using System.Linq;

namespace Minesweeper3D.Core
{
    /// <summary>
    /// Deterministic solver that reads partial game state and returns
    /// explainable deduction steps. Pure C#.
    /// </summary>
    public class Solver
    {
        /// <summary>
        /// Run one pass of all rules over every revealed cell.
        /// Returns deduction steps found. Empty list = solver stalled.
        /// </summary>
        public List<DeductionStep> SolveStep(Board board)
        {
            var steps = new List<DeductionStep>();
            var seen = new HashSet<Coord3>();  // avoid duplicate inferences

            int size = board.Size;
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = new Coord3(x, y, z);
                if (board.GetState(c) != CellState.Revealed) continue;
                if (board.GetCount(c) == 0) continue;

                // R1: all hidden are mines
                foreach (var step in Rules.AllHiddenAreMines(board, c))
                {
                    var novel = step.AffectedCells.Where(a => !seen.Contains(a)).ToArray();
                    if (novel.Length > 0)
                    {
                        step.AffectedCells = novel;
                        steps.Add(step);
                        foreach (var a in novel) seen.Add(a);
                    }
                }

                // R2: all remaining are safe
                foreach (var step in Rules.AllRemainingAreSafe(board, c))
                {
                    var novel = step.AffectedCells.Where(a => !seen.Contains(a)).ToArray();
                    if (novel.Length > 0)
                    {
                        step.AffectedCells = novel;
                        steps.Add(step);
                        foreach (var a in novel) seen.Add(a);
                    }
                }
            }

            return steps;
        }

        /// <summary>
        /// Fully solve a board from the given first click.
        /// Returns all deduction steps in order.
        /// If the solver stalls with hidden safe cells remaining, returns what it found
        /// and sets <paramref name="solvedCompletely"/> to false.
        /// </summary>
        public List<DeductionStep> SolveFull(Board board, Coord3 firstClick, out bool solvedCompletely)
        {
            var allSteps = new List<DeductionStep>();

            // Initial reveal
            board.Reveal(firstClick);

            while (board.Status == GameStatus.Playing)
            {
                var steps = SolveStep(board);
                if (steps.Count == 0)
                {
                    // Stalled — board requires guessing
                    solvedCompletely = false;
                    return allSteps;
                }

                allSteps.AddRange(steps);

                // Apply deductions
                foreach (var step in steps)
                {
                    foreach (var cell in step.AffectedCells)
                    {
                        if (step.InferredMine)
                        {
                            // Flag it
                            if (board.GetState(cell) == CellState.Hidden)
                                board.ToggleFlag(cell);
                        }
                        else
                        {
                            // Reveal safe cell
                            board.Reveal(cell);
                        }
                    }
                }
            }

            solvedCompletely = board.Status == GameStatus.Won;
            return allSteps;
        }

        /// <summary>
        /// Validate that a board is no-guess solvable from the given first click.
        /// Does NOT mutate the original board — creates a copy internally.
        /// </summary>
        public bool ValidateNoGuess(int size, IEnumerable<Coord3> mineCoords, Coord3 firstClick)
        {
            var board = new Board(size, mineCoords);
            SolveFull(board, firstClick, out bool solved);
            return solved;
        }
    }
}
