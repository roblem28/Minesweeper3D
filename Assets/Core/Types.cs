namespace Minesweeper3D.Core
{
    /// <summary>Cell visibility state.</summary>
    public enum CellState
    {
        Hidden,
        Revealed,
        Flagged
    }

    /// <summary>Result of a Reveal operation.</summary>
    public enum RevealResult
    {
        Ok,
        Mine,
        AlreadyRevealed,
        Flagged,
        OutOfBounds
    }

    /// <summary>Overall game status.</summary>
    public enum GameStatus
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>Integer 3D coordinate.</summary>
    public readonly struct Coord3 : System.IEquatable<Coord3>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Coord3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Coord3 other) =>
            X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) =>
            obj is Coord3 c && Equals(c);

        public override int GetHashCode() =>
            (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);

        public static bool operator ==(Coord3 a, Coord3 b) => a.Equals(b);
        public static bool operator !=(Coord3 a, Coord3 b) => !a.Equals(b);

        public override string ToString() => $"({X},{Y},{Z})";
    }

    /// <summary>A single solver deduction step.</summary>
    public class DeductionStep
    {
        /// <summary>Rule identifier (e.g. "R1", "R2").</summary>
        public string RuleId;

        /// <summary>The cell(s) whose count drove this deduction.</summary>
        public Coord3[] SourceCells;

        /// <summary>Cells inferred by this step.</summary>
        public Coord3[] AffectedCells;

        /// <summary>What was inferred: true = MINE, false = SAFE.</summary>
        public bool InferredMine;

        public override string ToString()
        {
            var label = InferredMine ? "MINE" : "SAFE";
            return $"[{RuleId}] from {string.Join(",", SourceCells)} → " +
                   $"{string.Join(",", AffectedCells)} = {label}";
        }
    }
}
