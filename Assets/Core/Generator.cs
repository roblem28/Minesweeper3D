using System;
using System.Collections.Generic;

namespace Minesweeper3D.Core
{
    /// <summary>
    /// Generates 3D Minesweeper boards with first-click safety.
    /// Pure C#, no Unity dependencies.
    /// </summary>
    public static class Generator
    {
        /// <summary>
        /// Generate a board with mines placed randomly (seeded),
        /// guaranteeing first-click cell and its 6 face-neighbors are safe.
        /// </summary>
        private const int MaxRegenerationAttempts = 50;
        private const float MaxFirstClickRevealRatio = 0.40f;
        private const int MaxMineBoosts = 10;

        public static Board Generate(int size, int mineCount, Coord3 firstClick, int seed)
        {
            int total = size * size * size;
            // Exclusion set is firstClick + up to 6 neighbors = 7 cells
            int maxMines = total - 7;
            int currentMines = mineCount;

            for (int boost = 0; boost <= MaxMineBoosts; boost++)
            {
                for (int attempt = 0; attempt < MaxRegenerationAttempts; attempt++)
                {
                    var board = GenerateBoard(size, currentMines, firstClick, seed + attempt);

                    int totalSafe = total - currentMines;
                    int revealed = SimulateReveal(board, firstClick);
                    if (revealed <= (int)(totalSafe * MaxFirstClickRevealRatio))
                        return board;
                }

                // All attempts exceeded threshold — add more mines and retry
                if (currentMines + 1 <= maxMines)
                {
                    currentMines++;
                    seed += MaxRegenerationAttempts; // fresh seed range
                }
                else
                    break;
            }

            // Should be extremely rare — return last generated board
            return GenerateBoard(size, currentMines, firstClick, seed);
        }

        private static Board GenerateBoard(int size, int mineCount, Coord3 firstClick, int seed)
        {
            int total = size * size * size;

            // Build exclusion set: firstClick + its 6 face-neighbors
            var excluded = new HashSet<int>();
            var tempBoard = new Board(size, Array.Empty<Coord3>());

            excluded.Add(tempBoard.FlatIndex(firstClick));
            foreach (var n in tempBoard.GetNeighbors(firstClick))
                excluded.Add(tempBoard.FlatIndex(n));

            int available = total - excluded.Count;
            if (mineCount > available)
                throw new ArgumentException(
                    $"Cannot place {mineCount} mines with {excluded.Count} excluded cells " +
                    $"in a {size}^3 grid ({total} total, {available} available).");

            // Fisher-Yates on available indices
            var candidates = new List<int>(available);
            for (int i = 0; i < total; i++)
                if (!excluded.Contains(i))
                    candidates.Add(i);

            var rng = new Random(seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            // Take first mineCount indices
            var mineCoords = new List<Coord3>(mineCount);
            for (int i = 0; i < mineCount; i++)
            {
                int idx = candidates[i];
                mineCoords.Add(tempBoard.FromFlat(idx));
            }

            return new Board(size, mineCoords);
        }

        /// <summary>
        /// Simulate a reveal at the given coord and return how many safe cells would be revealed.
        /// Does not mutate the board — creates a temporary copy for simulation.
        /// </summary>
        private static int SimulateReveal(Board board, Coord3 click)
        {
            // Create a duplicate board for simulation
            int size = board.Size;
            var mineCoords = new List<Coord3>();
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = new Coord3(x, y, z);
                if (board.IsMine(c)) mineCoords.Add(c);
            }

            var simBoard = new Board(size, mineCoords);
            simBoard.Reveal(click);
            return simBoard.RevealedSafeCount;
        }
    }
}
