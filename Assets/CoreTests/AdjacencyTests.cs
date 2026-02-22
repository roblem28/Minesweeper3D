using NUnit.Framework;
using Minesweeper3D.Core;

namespace Minesweeper3D.CoreTests
{
    [TestFixture]
    public class AdjacencyTests
    {
        [Test]
        public void CenterCell_Has6Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(1, 1, 1));
            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void CornerCell_Has3Neighbors()
        {
            var board = new Board(4, System.Array.Empty<Coord3>());
            var neighbors = board.GetNeighbors(new Coord3(0, 0, 0));
            Assert.AreEqual(3, neighbors.Count);
        }

        [Test]
        public void NeighborCount_CorrectForAdjacentMine()
        {
            // Mine at (1,1,1). Only the 6 face neighbors count it.
            var mines = new[] { new Coord3(1, 1, 1) };
            var board = new Board(3, mines);

            // Face neighbors of (1,1,1) should have count 1
            Assert.AreEqual(1, board.GetCount(new Coord3(0, 1, 1))); // -x
            Assert.AreEqual(1, board.GetCount(new Coord3(2, 1, 1))); // +x
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 0, 1))); // -y
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 2, 1))); // +y
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 0))); // -z
            Assert.AreEqual(1, board.GetCount(new Coord3(1, 1, 2))); // +z

            // Diagonal neighbor should have count 0
            Assert.AreEqual(0, board.GetCount(new Coord3(0, 0, 0)),
                "Diagonal cell should NOT count mine");
            Assert.AreEqual(0, board.GetCount(new Coord3(2, 2, 2)),
                "Diagonal cell should NOT count mine");
        }

        [Test]
        public void FloodFill_WorksCorrectly()
        {
            // No mines: reveal one cell should flood-fill entire board
            var board = new Board(3, System.Array.Empty<Coord3>());
            board.Reveal(new Coord3(1, 1, 1));

            for (int z = 0; z < 3; z++)
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.AreEqual(CellState.Revealed, board.GetState(new Coord3(x, y, z)),
                    $"Cell ({x},{y},{z}) should be revealed by flood fill");

            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void GeneratorFirstClickSafety()
        {
            // First-click exclusion zone is 7 cells (self + 6 faces)
            var firstClick = new Coord3(2, 2, 2);
            var board = Generator.Generate(4, 5, firstClick, 42);

            // First click cell and its 6 face neighbors should be safe
            Assert.IsFalse(board.IsMine(firstClick));
            foreach (var n in board.GetNeighbors(firstClick))
                Assert.IsFalse(board.IsMine(n),
                    $"Face neighbor {n} of first click should be safe");
        }
    }
}
