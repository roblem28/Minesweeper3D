using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Minesweeper3D.Core;

namespace Minesweeper3D.CoreTests
{
    [TestFixture]
    public class BoardTests
    {
        // --- Adjacency / Neighbor Counts ---

        [Test]
        public void CornerCell_Has7Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(0, 0, 0));
            Assert.AreEqual(7, neighbors.Count);
        }

        [Test]
        public void EdgeCell_Has11Neighbors()
        {
            // Edge: one axis at boundary, other two interior (for size >= 4)
            var board = new Board(4, System.Array.Empty<Coord3>());
            // (0,1,1) — x=0 boundary, y and z interior
            var neighbors = board.GetNeighbors(new Coord3(0, 1, 1));
            Assert.AreEqual(17, neighbors.Count);
            // Actually for a face cell (one axis at 0), count is 17.
            // True edge (two axes at boundary): (0,0,1)
            var edgeNeighbors = board.GetNeighbors(new Coord3(0, 0, 1));
            Assert.AreEqual(11, edgeNeighbors.Count);
        }

        [Test]
        public void FaceCell_Has17Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            // (0,1,1) — only x at boundary
            var neighbors = board.GetNeighbors(new Coord3(0, 1, 1));
            Assert.AreEqual(17, neighbors.Count);
        }

        [Test]
        public void CenterCell_Has26Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(1, 1, 1));
            Assert.AreEqual(26, neighbors.Count);
        }

        [Test]
        public void NeighborCount_SingleMine_CorrectForAdjacentCells()
        {
            // Place one mine at (1,1,1) in a 3x3x3 grid
            var mines = new[] { new Coord3(1, 1, 1) };
            var board = new Board(3, mines);

            // All 26 neighbors of (1,1,1) should have count == 1
            var neighbors = board.GetNeighbors(new Coord3(1, 1, 1));
            foreach (var n in neighbors)
            {
                Assert.AreEqual(1, board.GetCount(n),
                    $"Cell {n} should have count 1 (adjacent to single mine at center)");
            }

            // The mine cell itself: count = 0 (it's a mine, count is of neighboring mines)
            // Actually count should reflect neighboring mines, but (1,1,1) has no mine neighbors
            Assert.AreEqual(0, board.GetCount(new Coord3(1, 1, 1)));
        }

        // --- Reveal / Flood Fill ---

        [Test]
        public void Reveal_SafeCell_ReturnsOk()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            var result = board.Reveal(new Coord3(0, 0, 0));
            Assert.AreEqual(RevealResult.Ok, result);
        }

        [Test]
        public void Reveal_MineCell_ReturnsMinAndLoses()
        {
            var mines = new[] { new Coord3(0, 0, 0) };
            var board = new Board(3, mines);
            var result = board.Reveal(new Coord3(0, 0, 0));
            Assert.AreEqual(RevealResult.Mine, result);
            Assert.AreEqual(GameStatus.Lost, board.Status);
        }

        [Test]
        public void Reveal_AlreadyRevealed_ReturnsAlreadyRevealed()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.Reveal(new Coord3(1, 1, 1));
            var result = board.Reveal(new Coord3(1, 1, 1));
            Assert.AreEqual(RevealResult.AlreadyRevealed, result);
        }

        [Test]
        public void FloodFill_NoMines_RevealsEntireBoard()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.Reveal(new Coord3(0, 0, 0));

            // All cells should be revealed
            for (int z = 0; z < 3; z++)
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.AreEqual(CellState.Revealed, board.GetState(new Coord3(x, y, z)),
                    $"Cell ({x},{y},{z}) should be revealed after flood fill with no mines");

            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void FloodFill_StopsAtNumberedCells()
        {
            // 4x4x4 grid, mine at (3,3,3)
            // Reveal (0,0,0) — flood fill should stop at cells with count > 0
            var mines = new[] { new Coord3(3, 3, 3) };
            var board = new Board(4, mines);
            board.Reveal(new Coord3(0, 0, 0));

            // (3,3,3) should NOT be revealed (it's a mine)
            Assert.AreEqual(CellState.Hidden, board.GetState(new Coord3(3, 3, 3)));

            // Cells adjacent to the mine (count > 0) should be revealed by flood fill
            // since they're reached but not expanded
            var mineNeighbors = board.GetNeighbors(new Coord3(3, 3, 3));
            foreach (var n in mineNeighbors)
            {
                Assert.AreEqual(CellState.Revealed, board.GetState(n),
                    $"Cell {n} adjacent to mine should still be revealed (flood fill reveals numbered cells, just doesn't expand through them)");
            }
        }

        // --- Flagging ---

        [Test]
        public void ToggleFlag_HiddenCell_BecomesFlagged()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            bool toggled = board.ToggleFlag(new Coord3(0, 0, 0));
            Assert.IsTrue(toggled);
            Assert.AreEqual(CellState.Flagged, board.GetState(new Coord3(0, 0, 0)));
        }

        [Test]
        public void ToggleFlag_FlaggedCell_BecomesHidden()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.ToggleFlag(new Coord3(0, 0, 0));
            board.ToggleFlag(new Coord3(0, 0, 0));
            Assert.AreEqual(CellState.Hidden, board.GetState(new Coord3(0, 0, 0)));
        }

        [Test]
        public void ToggleFlag_RevealedCell_ReturnsFalse()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.Reveal(new Coord3(1, 1, 1));
            bool toggled = board.ToggleFlag(new Coord3(1, 1, 1));
            Assert.IsFalse(toggled);
        }

        [Test]
        public void Reveal_FlaggedCell_ReturnsFlagged()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.ToggleFlag(new Coord3(0, 0, 0));
            var result = board.Reveal(new Coord3(0, 0, 0));
            Assert.AreEqual(RevealResult.Flagged, result);
        }

        // --- Bounds ---

        [Test]
        public void Reveal_OutOfBounds_ReturnsOutOfBounds()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            Assert.AreEqual(RevealResult.OutOfBounds, board.Reveal(new Coord3(-1, 0, 0)));
            Assert.AreEqual(RevealResult.OutOfBounds, board.Reveal(new Coord3(3, 0, 0)));
        }

        // --- Win Detection ---

        [Test]
        public void Win_AllSafeCellsRevealed()
        {
            // 2x2x2 with 1 mine — reveal all 7 safe cells
            var mines = new[] { new Coord3(0, 0, 0) };
            var board = new Board(2, mines);

            // Reveal all non-mine cells
            for (int z = 0; z < 2; z++)
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                board.Reveal(new Coord3(x, y, z));
            }

            Assert.AreEqual(GameStatus.Won, board.Status);
        }
    }
}
