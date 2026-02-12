using System;
using System.Collections.Generic;

namespace Minesweeper3D.Core
{
    /// <summary>
    /// Pure C# NxNxN 3D Minesweeper board.
    /// No Unity dependencies.
    /// </summary>
    public class Board
    {
        public readonly int Size;
        public GameStatus Status { get; private set; }

        // Flat arrays indexed by FlatIndex(x,y,z)
        private readonly bool[] _mines;
        private readonly CellState[] _states;
        private readonly int[] _counts;  // neighbor mine counts

        private int _revealedSafeCount;
        private int _revealedTotalCount;
        private readonly int _totalCells;
        private readonly int _totalSafe;

        /// <summary>Create a board of given size with mines pre-placed.</summary>
        /// <param name="size">Side length (NxNxN).</param>
        /// <param name="mineCoords">Coordinates of mines.</param>
        public Board(int size, IEnumerable<Coord3> mineCoords)
        {
            if (size < 1)
                throw new ArgumentException("Size must be >= 1", nameof(size));

            Size = size;
            int total = size * size * size;
            _totalCells = total;
            _mines = new bool[total];
            _states = new CellState[total];
            _counts = new int[total];
            Status = GameStatus.Playing;

            int mineCount = 0;
            foreach (var c in mineCoords)
            {
                if (!InBounds(c))
                    throw new ArgumentException($"Mine coord {c} out of bounds for size {size}");
                int idx = FlatIndex(c);
                if (!_mines[idx])
                {
                    _mines[idx] = true;
                    mineCount++;
                }
            }

            _mineCount = mineCount;
            _totalSafe = total - mineCount;
            ComputeCounts();
        }

        // --- Public API ---

        public bool InBounds(Coord3 c) =>
            c.X >= 0 && c.X < Size &&
            c.Y >= 0 && c.Y < Size &&
            c.Z >= 0 && c.Z < Size;

        public bool IsMine(Coord3 c) => _mines[FlatIndex(c)];
        public CellState GetState(Coord3 c) => _states[FlatIndex(c)];
        public int GetCount(Coord3 c) => _counts[FlatIndex(c)];
        private int _mineCount;
        public int MineCount => _mineCount;
        public int TotalCells => _totalCells;
        public int TotalSafeCells => _totalSafe;
        public int RevealedSafeCount => _revealedSafeCount;
        public int RevealedTotalCount => _revealedTotalCount;
        public int HiddenCount => _totalCells - _revealedTotalCount;
        public int SafeLeft => _totalSafe - _revealedSafeCount;

        /// <summary>
        /// Single-click reveal action. Returns the result.
        /// If count == 0 and not a mine, triggers 3D flood-fill.
        /// </summary>
        public RevealResult Reveal(Coord3 c)
        {
            if (!InBounds(c)) return RevealResult.OutOfBounds;
            if (Status != GameStatus.Playing) return RevealResult.AlreadyRevealed;

            int idx = FlatIndex(c);
            if (_states[idx] == CellState.Revealed) return RevealResult.AlreadyRevealed;
            if (_states[idx] == CellState.Flagged) return RevealResult.Flagged;

            if (_mines[idx])
            {
                _states[idx] = CellState.Revealed;
                _revealedTotalCount++;
                Status = GameStatus.Lost;
                return RevealResult.Mine;
            }

            FloodFill(c);
            CheckWin();
            return RevealResult.Ok;
        }

        /// <summary>
        /// Double-click chord action on an already revealed numbered cell.
        /// If adjacent flagged count equals this cell's number, reveals all adjacent
        /// hidden, non-flagged neighbors via <see cref="Reveal(Coord3)"/>.
        /// </summary>
        public RevealResult ChordReveal(Coord3 c)
        {
            if (!InBounds(c)) return RevealResult.OutOfBounds;
            if (Status != GameStatus.Playing) return RevealResult.AlreadyRevealed;

            int idx = FlatIndex(c);
            if (_states[idx] != CellState.Revealed) return RevealResult.AlreadyRevealed;

            int requiredFlags = _counts[idx];
            if (requiredFlags <= 0) return RevealResult.AlreadyRevealed;

            int flaggedCount = 0;
            foreach (var n in EnumerateNeighbors(c))
            {
                if (_states[FlatIndex(n)] == CellState.Flagged)
                    flaggedCount++;
            }

            if (flaggedCount != requiredFlags)
                return RevealResult.AlreadyRevealed;

            foreach (var n in EnumerateNeighbors(c))
            {
                int ni = FlatIndex(n);
                if (_states[ni] != CellState.Hidden) continue;

                var revealResult = Reveal(n);
                if (revealResult == RevealResult.Mine)
                    return RevealResult.Mine;
            }

            return RevealResult.Ok;
        }

        /// <summary>Toggle flag on a hidden cell.</summary>
        public bool ToggleFlag(Coord3 c)
        {
            if (!InBounds(c) || Status != GameStatus.Playing) return false;
            int idx = FlatIndex(c);

            switch (_states[idx])
            {
                case CellState.Hidden:
                    _states[idx] = CellState.Flagged;
                    return true;
                case CellState.Flagged:
                    _states[idx] = CellState.Hidden;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Get all 26-adjacent neighbors in bounds.</summary>
        public List<Coord3> GetNeighbors(Coord3 c)
        {
            var result = new List<Coord3>(26);
            foreach (var n in EnumerateNeighbors(c))
                result.Add(n);
            return result;
        }

        /// <summary>
        /// Single source of truth for 3x3x3-minus-self neighbor traversal.
        /// </summary>
        private IEnumerable<Coord3> EnumerateNeighbors(Coord3 c)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                var n = new Coord3(c.X + dx, c.Y + dy, c.Z + dz);
                if (InBounds(n))
                    yield return n;
            }
        }

        /// <summary>Total number of mines on the board.</summary>
        public int TotalMines() => _mineCount;

        // --- Internals ---

        internal int FlatIndex(Coord3 c) => c.X + Size * (c.Y + Size * c.Z);

        internal Coord3 FromFlat(int idx)
        {
            int x = idx % Size;
            int y = (idx / Size) % Size;
            int z = idx / (Size * Size);
            return new Coord3(x, y, z);
        }

        private void ComputeCounts()
        {
            int total = Size * Size * Size;
            for (int i = 0; i < total; i++)
            {
                var c = FromFlat(i);
                int count = 0;
                foreach (var n in EnumerateNeighbors(c))
                    if (_mines[FlatIndex(n)]) count++;
                _counts[i] = count;
            }
        }

        private void FloodFill(Coord3 start)
        {
            var queue = new Queue<Coord3>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int idx = FlatIndex(c);

                if (_states[idx] == CellState.Revealed) continue;
                if (_mines[idx]) continue;

                _states[idx] = CellState.Revealed;
                _revealedSafeCount++;
                _revealedTotalCount++;

                // Only expand if this cell's count is 0
                if (_counts[idx] == 0)
                {
                    foreach (var n in EnumerateNeighbors(c))
                    {
                        int ni = FlatIndex(n);
                        if (_states[ni] == CellState.Hidden && !_mines[ni])
                            queue.Enqueue(n);
                    }
                }
            }
        }

        private void CheckWin()
        {
            if (_revealedSafeCount >= _totalSafe)
                Status = GameStatus.Won;
        }
    }
}
