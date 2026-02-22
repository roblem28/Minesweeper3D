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
        private readonly int _totalSafe;
        private int _flagCount;
        private int _correctFlagCount;

        // The 6 face-adjacent directions (±1 on exactly one axis)
        private static readonly int[,] Faces6Dirs =
        {
            { 1, 0, 0}, {-1, 0, 0},
            { 0, 1, 0}, { 0,-1, 0},
            { 0, 0, 1}, { 0, 0,-1}
        };

        /// <summary>Create a board of given size with mines pre-placed.</summary>
        /// <param name="size">Side length (NxNxN).</param>
        /// <param name="mineCoords">Coordinates of mines.</param>
        public Board(int size, IEnumerable<Coord3> mineCoords)
        {
            if (size < 1)
                throw new ArgumentException("Size must be >= 1", nameof(size));

            Size = size;
            int total = size * size * size;
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
        public int FlagCount => _flagCount;
        public int RevealedSafeCount => _revealedSafeCount;
        public int TotalSafe => _totalSafe;

        /// <summary>
        /// Reveal a cell. Returns the result.
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
                Status = GameStatus.Lost;
                return RevealResult.Mine;
            }

            FloodFill(c);
            CheckWin();
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
                    _flagCount++;
                    if (_mines[idx]) _correctFlagCount++;
                    CheckWin();
                    return true;
                case CellState.Flagged:
                    _states[idx] = CellState.Hidden;
                    _flagCount--;
                    if (_mines[idx]) _correctFlagCount--;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Get face-adjacent neighbors (6-neighbor mode, no diagonals).</summary>
        public List<Coord3> GetNeighbors(Coord3 c)
        {
            var result = new List<Coord3>(6);
            for (int i = 0; i < 6; i++)
            {
                var n = new Coord3(c.X + Faces6Dirs[i, 0],
                                   c.Y + Faces6Dirs[i, 1],
                                   c.Z + Faces6Dirs[i, 2]);
                if (InBounds(n))
                    result.Add(n);
            }
            return result;
        }

        /// <summary>Total number of mines on the board.</summary>
        public int TotalMines()
        {
            int count = 0;
            for (int i = 0; i < _mines.Length; i++)
                if (_mines[i]) count++;
            return count;
        }

        /// <summary>
        /// Chord: if a revealed cell's count equals its flagged neighbor count,
        /// reveal all unflagged hidden neighbors. May trigger flood-fill or loss.
        /// </summary>
        public bool ChordReveal(Coord3 c)
        {
            if (!InBounds(c) || Status != GameStatus.Playing) return false;
            int idx = FlatIndex(c);
            if (_states[idx] != CellState.Revealed) return false;

            int count = _counts[idx];
            if (count == 0) return false;

            int flagged = CountFlaggedNeighbors(c);
            if (flagged != count) return false;

            bool anyRevealed = false;
            foreach (var n in GetNeighbors(c))
            {
                int ni = FlatIndex(n);
                if (_states[ni] != CellState.Hidden) continue;

                if (_mines[ni])
                {
                    _states[ni] = CellState.Revealed;
                    Status = GameStatus.Lost;
                    anyRevealed = true;
                }
                else
                {
                    FloodFill(n);
                    anyRevealed = true;
                }
            }

            if (anyRevealed && Status == GameStatus.Playing)
                CheckWin();

            return anyRevealed;
        }

        /// <summary>Count flagged neighbors of a cell.</summary>
        public int CountFlaggedNeighbors(Coord3 c)
        {
            int flagged = 0;
            foreach (var n in GetNeighbors(c))
            {
                if (_states[FlatIndex(n)] == CellState.Flagged)
                    flagged++;
            }
            return flagged;
        }

        /// <summary>Check if any cross-slice (dz != 0) neighbor is Hidden or Flagged.</summary>
        public void GetCrossSliceStatus(Coord3 c, out bool hasAbove, out bool hasBelow)
        {
            hasAbove = false;
            hasBelow = false;
            foreach (var n in GetNeighbors(c))
            {
                int dz = n.Z - c.Z;
                if (dz == 0) continue;
                var state = _states[FlatIndex(n)];
                if (state == CellState.Hidden || state == CellState.Flagged)
                {
                    if (dz > 0) hasAbove = true;
                    else hasBelow = true;
                }
                if (hasAbove && hasBelow) return;
            }
        }

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
                foreach (var n in GetNeighbors(c))
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

                // Only expand if this cell's count is 0
                if (_counts[idx] == 0)
                {
                    foreach (var n in GetNeighbors(c))
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
            {
                Status = GameStatus.Won;
                return;
            }

            // Win by flagging: all mines flagged, no incorrect flags
            if (_correctFlagCount == _mineCount && _flagCount == _mineCount)
                Status = GameStatus.Won;
        }
    }
}
