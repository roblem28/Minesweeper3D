using UnityEngine;

namespace Minesweeper3D.Unity
{
    public static class ScoreController
    {
        public static int Calculate(int gridSize, float elapsedSeconds, int hintsUsed)
        {
            int baseScore = gridSize * gridSize * gridSize * 1000;
            int timePenalty = Mathf.RoundToInt(elapsedSeconds * 10f);
            int hintPenalty = hintsUsed * 500;
            return Mathf.Max(0, baseScore - timePenalty - hintPenalty);
        }
    }
}
