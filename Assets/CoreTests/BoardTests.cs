using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Minesweeper3D.Core;

namespace Minesweeper3D.CoreTests
{
    [TestFixture]
    public class BoardTests
    {
        // --- Adjacency / Neighbor Counts (6-neighbor, faces only) ---

        [Test]
        public void CornerCell_Has3Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(0, 0, 0));
            Assert.AreEqual(3, neighbors.Count);
        }

        [Test]
        public void EdgeCell_Has4Neighbors()
        {
            // Edge: two axes at boundary, one interior
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(0, 0, 1));
            Assert.AreEqual(4, neighbors.Count);
        }

        [Test]
        public void FaceCell_Has5Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            // (0,1,1) — only x at boundary
            var neighbors = board.GetNeighbors(new Coord3(0, 1, 1));
            Assert.AreEqual(5, neighbors.Count);
        }

        [Test]
        public void CenterCell_Has6Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(1, 1, 1));
            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void NeighborCount_SingleMine_CorrectForFaceAdjacentCells()
        {
            // Place one mine at (1,1,1) in a 3x3x3 grid
            var mines = new[] { new Coord3(1, 1, 1) };
            var board = new Board(3, mines);

            // 6 face neighbors of (1,1,1) should have count == 1
            Assert.AreEqual(1, board.GetCount(new Coord3(0, 1, 1))); // -x
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 1, 1))); // +x
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 0, 1))); // -y
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 2, 1))); // +y
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 0))); // -z
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 2))); // +z

            // Diagonal cells should NOT count the mine
            Assert.AreEqual(0, board.GetCount(new Coord3(0, 0, 0)),
                "Diagonal cell should NOT count mine in 6-neighbor mode");
            Assert.AreEqual(0, board.GetCount(new Coord3(2, 2, 2)),
                "Diagonal cell should NOT count mine in 6-neighbor mode");

            // The mine cell itself: no neighboring mines
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
            var mines = new[] { new Coord3(3, 3, 3) };
            var board = new Board(4, mines);
            board.Reveal(new Coord3(0, 0, 0));

            // (3,3,3) should NOT be revealed (it's a mine)
            Assert.AreEqual(CellState.Hidden, board.GetState(new Coord3(3, 3, 3)));

            // Face neighbors of the mine should be revealed (flood fill reveals numbered cells)
            foreach (var n in board.GetNeighbors(new Coord3(3, 3, 3)))
            {
                Assert.AreEqual(CellState.Revealed, board.GetState(n),
                    $"Cell {n} adjacent to mine should be revealed by flood fill");
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
        public void CrossLayer_MineDirectlyAbove_Counted()
        {
            // Mine at (2,2,3), cell at (2,2,2) — face-adjacent on z-axis
            var mines = new[] { new Coord3(2, 2, 3) };
            var board = new Board(4, mines);
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 2, 2)),
                "Cell directly below mine (dz=1) must count it");
        }

        [Test]
        public void CrossLayer_MineDirectlyBelow_Counted()
        {
            // Mine at (2,2,1), cell at (2,2,2) — face-adjacent on z-axis
            var mines = new[] { new Coord3(2, 2, 1) };
            var board = new Board(4, mines);
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 2, 2)),
                "Cell directly above mine (dz=-1) must count it");
        }

        [Test]
        public void CrossLayer_DiagonalMine_NotCounted()
        {
            // Mine at (0,0,0), cell at (1,1,1) — diagonal, NOT face-adjacent
            var mines = new[] { new Coord3(0, 0, 0) };
            var board = new Board(4, mines);
            Assert.AreEqual(0, board.GetCount(new Coord3(1, 1, 1)),
                "Diagonal mine should NOT be counted in 6-neighbor mode");
        }

        [Test]
        public void CrossLayer_FaceAdjacentMines_AllCounted()
        {
            // Place mines on all 6 face directions from (2,2,2)
            var mines = new[]
            {
                new Coord3(1, 2, 2), // -x
                new Coord3(3, 2, 2), // +x
                new Coord3(2, 1, 2), // -y
                new Coord3(2, 3, 2), // +y
                new Coord3(2, 2, 1), // -z
                new Coord3(2, 2, 3), // +z
            };
            var board = new Board(4, mines);
            Assert.AreEqual(6, board.GetCount(new Coord3(2, 2, 2)),
                "Cell (2,2,2) should count all 6 face-adjacent mines");
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

        [Test]
        public void Win_AllMinesCorrectlyFlagged()
        {
            // 3x3x3 with 2 mines — flag both mines, no wrong flags → Win
            var mines = new[] { new Coord3(0, 0, 0), new Coord3(2, 2, 2) };
            var board = new Board(3, mines);

            Assert.AreEqual(GameStatus.Playing, board.Status);
            board.ToggleFlag(new Coord3(0, 0, 0));
            Assert.AreEqual(GameStatus.Playing, board.Status); // only 1 of 2 flagged
            board.ToggleFlag(new Coord3(2, 2, 2));
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void NoWin_WrongFlag()
        {
            // 3x3x3 with 1 mine — flag the mine AND a non-mine → Playing
            var mines = new[] { new Coord3(0, 0, 0) };
            var board = new Board(3, mines);

            board.ToggleFlag(new Coord3(1, 1, 1)); // incorrect flag first
            board.ToggleFlag(new Coord3(0, 0, 0)); // correct flag
            Assert.AreEqual(GameStatus.Playing, board.Status,
                "Flagging a non-mine cell should prevent flag-win even if all mines are flagged");
        }

        [Test]
        public void NoWin_PartialFlags()
        {
            // 3x3x3 with 3 mines — flag only 2 of 3 → Playing
            var mines = new[] { new Coord3(0, 0, 0), new Coord3(1, 1, 1), new Coord3(2, 2, 2) };
            var board = new Board(3, mines);

            board.ToggleFlag(new Coord3(0, 0, 0));
            board.ToggleFlag(new Coord3(1, 1, 1));
            Assert.AreEqual(GameStatus.Playing, board.Status,
                "Flagging only some mines should not trigger win");
        }

        // --- Chord Reveal ---

        [Test]
        public void ChordReveal_CorrectFlags_RevealsNeighbors()
        {
            // 4x4x4, mines at (1,1,0) and (3,3,3). Cell (1,1,1) is face-adjacent to first mine, count=1.
            // Two mines so flagging one doesn't trigger win-by-flagging.
            var mines = new[] { new Coord3(1, 1, 0), new Coord3(3, 3, 3) };
            var board = new Board(4, mines);

            // Reveal (1,1,1) — count is 1 so flood fill won't expand
            board.Reveal(new Coord3(1, 1, 1));
            Assert.AreEqual(CellState.Revealed, board.GetState(new Coord3(1, 1, 1)));
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 1)));

            board.ToggleFlag(new Coord3(1, 1, 0));
            Assert.AreEqual(GameStatus.Playing, board.Status); // still playing (2nd mine unflagged)
            bool result = board.ChordReveal(new Coord3(1, 1, 1));
            Assert.IsTrue(result);

            // All non-mine face-neighbors of (1,1,1) should be revealed
            foreach (var n in board.GetNeighbors(new Coord3(1, 1, 1)))
            {
                if (n.Equals(new Coord3(1, 1, 0))) continue; // mine, flagged
                Assert.AreEqual(CellState.Revealed, board.GetState(n),
                    $"Neighbor {n} should be revealed after chord");
            }
        }

        [Test]
        public void ChordReveal_WrongFlag_CausesLoss()
        {
            // 4x4x4, mine at (1,1,0). Cell (1,1,1) has count=1.
            // Flag (1,1,2) (wrong! — face-neighbor but not the mine). Chord (1,1,1).
            // This should reveal (1,1,0) which is a mine → loss.
            var mines = new[] { new Coord3(1, 1, 0) };
            var board = new Board(4, mines);
            board.Reveal(new Coord3(1, 1, 1));
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 1)));

            board.ToggleFlag(new Coord3(1, 1, 2)); // wrong flag (face-neighbor but not the mine)
            bool result = board.ChordReveal(new Coord3(1, 1, 1));
            Assert.IsTrue(result);
            Assert.AreEqual(GameStatus.Lost, board.Status);
        }

        [Test]
        public void ChordReveal_NotEnoughFlags_DoesNothing()
        {
            // 4x4x4, mine at (1,1,0). Cell (1,1,1) has count=1. Don't flag.
            var mines = new[] { new Coord3(1, 1, 0) };
            var board = new Board(4, mines);
            board.Reveal(new Coord3(1, 1, 1));

            bool result = board.ChordReveal(new Coord3(1, 1, 1));
            Assert.IsFalse(result, "Chord should not trigger without enough flags");
        }

        // --- CountFlaggedNeighbors ---

        [Test]
        public void CountFlaggedNeighbors_ReturnsCorrectCount()
        {
            var board = new Board(3, System.Array.Empty<Coord3>());
            // Flag face-neighbors of (1,1,1)
            board.ToggleFlag(new Coord3(0, 1, 1)); // -x face
            board.ToggleFlag(new Coord3(2, 1, 1)); // +x face
            board.ToggleFlag(new Coord3(1, 0, 1)); // -y face

            // (1,1,1) has 6 face neighbors, 3 are flagged
            Assert.AreEqual(3, board.CountFlaggedNeighbors(new Coord3(1, 1, 1)));

            // Diagonal flag should NOT be counted
            board.ToggleFlag(new Coord3(0, 0, 0)); // diagonal of (1,1,1)
            Assert.AreEqual(3, board.CountFlaggedNeighbors(new Coord3(1, 1, 1)),
                "Diagonal flags should not be counted in 6-neighbor mode");
        }

        // --- GetCrossSliceStatus ---

        [Test]
        public void GetCrossSliceStatus_DetectsAboveAndBelow()
        {
            // 4x4x4, all cells hidden. Cell (1,1,1) has face-neighbors on z=0 and z=2.
            var board = new Board(4, System.Array.Empty<Coord3>());
            board.GetCrossSliceStatus(new Coord3(1, 1, 1), out bool hasAbove, out bool hasBelow);
            Assert.IsTrue(hasAbove, "Should detect hidden cells on z=2 (above)");
            Assert.IsTrue(hasBelow, "Should detect hidden cells on z=0 (below)");

            // Corner cell (0,0,0) has face-neighbor on z=1 (above) only
            board.GetCrossSliceStatus(new Coord3(0, 0, 0), out hasAbove, out hasBelow);
            Assert.IsTrue(hasAbove, "Should detect hidden cells above corner");
            Assert.IsFalse(hasBelow, "No cells below z=0");
        }
    }
}
