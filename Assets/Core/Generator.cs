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
        /// guaranteeing first-click cell and its 26 neighbors are safe.
        /// </summary>
        /// <param name="size">Side length N for NxNxN grid.</param>
        /// <param name="mineCount">Number of mines to place.</param>
        /// <param name="firstClick">First click coordinate (protected zone).</param>
        /// <param name="seed">RNG seed for determinism.</param>
        /// <returns>A new Board instance.</returns>
        public static Board Generate(int size, int mineCount, Coord3 firstClick, int seed)
        {
            int total = size * size * size;

            // Build exclusion set: firstClick + its 26 neighbors
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
    }
}
