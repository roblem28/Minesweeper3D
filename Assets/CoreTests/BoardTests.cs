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

        // --- Cross-layer adjacency ---

        [Test]
        public void CrossLayer_MinesOnDifferentZLayers_CountedCorrectly()
        {
            // 4x4x4 grid. Place mines on z=0 and z=2, check cell at z=1 counts both.
            // Mine A at (1,1,0) — directly below (1,1,1) on z-axis
            // Mine B at (2,2,2) — diagonal neighbor of (1,1,1) across z
            // Mine C at (0,0,0) — diagonal neighbor of (1,1,1) across z
            var mines = new[]
            {
                new Coord3(1, 1, 0),  // directly below center on z
                new Coord3(2, 2, 2),  // diagonal above
                new Coord3(0, 0, 0),  // diagonal below
            };
            var board = new Board(4, mines);

            // Cell (1,1,1) should see all 3 mines as neighbors
            int count = board.GetCount(new Coord3(1, 1, 1));
            Assert.AreEqual(3, count,
                "Cell (1,1,1) should count mines at (1,1,0), (2,2,2), and (0,0,0) as neighbors");

            // Cell (1,1,0) is a mine — its count should include mine at (0,0,0) [diagonal] but NOT (2,2,2) [too far]
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 0)),
                "Mine at (1,1,0) should see 1 neighboring mine: (0,0,0)");

            // Cell (1,1,2) should see mines at (2,2,2) [diagonal dz=0 same layer? no, dz=0 for z=2]
            // Wait: (1,1,2) neighbors include (2,2,2) [dx=1,dy=1,dz=0] — but that's same layer not cross.
            // (1,1,2) also neighbors (1,1,0)? No: dz = 0-2 = -2, out of range.
            // So (1,1,2) sees only (2,2,2) as mine neighbor (dx=1,dy=1,dz=0 — same layer).
            // Actually (2,2,2) is at z=2, (1,1,2) is at z=2: dz=0, dx=1, dy=1 → neighbor. Count = 1.
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 2)),
                "Cell (1,1,2) should see 1 mine: (2,2,2) same-layer diagonal");
        }

        [Test]
        public void CrossLayer_MineDirectlyAbove_Counted()
        {
            // Simple test: mine at (2,2,3), cell at (2,2,2) should count it (dz=+1)
            var mines = new[] { new Coord3(2, 2, 3) };
            var board = new Board(4, mines);
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 2, 2)),
                "Cell directly below mine (dz=1) must count it");
        }

        [Test]
        public void CrossLayer_MineDirectlyBelow_Counted()
        {
            // Mine at (2,2,1), cell at (2,2,2) should count it (dz=-1)
            var mines = new[] { new Coord3(2, 2, 1) };
            var board = new Board(4, mines);
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 2, 2)),
                "Cell directly above mine (dz=-1) must count it");
        }

        [Test]
        public void CrossLayer_AllDzDirections_CountedFromCenter()
        {
            // Place mines in all 3 z-layers adjacent to (2,2,2): z=1, z=2, z=3
            // One mine per layer, all should be counted
            var mines = new[]
            {
                new Coord3(1, 1, 1),  // dz=-1, dx=-1, dy=-1
                new Coord3(3, 3, 2),  // dz=0, dx=+1, dy=+1 (same layer)
                new Coord3(2, 3, 3),  // dz=+1, dx=0, dy=+1
            };
            var board = new Board(4, mines);
            Assert.AreEqual(3, board.GetCount(new Coord3(2, 2, 2)),
                "Cell (2,2,2) should count all 3 mines across z-layers");
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
