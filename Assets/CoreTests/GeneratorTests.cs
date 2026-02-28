using System.Linq;
using NUnit.Framework;
using Minesweeper3D.Core;

namespace Minesweeper3D.CoreTests
{
    [TestFixture]
    public class GeneratorTests
    {
        [Test]
        public void FirstClick_IsNotMine()
        {
            var click = new Coord3(2, 2, 2);
            for (int seed = 0; seed < 100; seed++)
            {
                var board = Generator.Generate(6, 10, click, seed);
                Assert.IsFalse(board.IsMine(click),
                    $"First click should never be a mine (seed={seed})");
            }
        }

        [Test]
        public void FirstClick_NeighborsNotMines()
        {
            var click = new Coord3(2, 2, 2);
            for (int seed = 0; seed < 100; seed++)
            {
                var board = Generator.Generate(6, 10, click, seed);
                var neighbors = board.GetNeighbors(click);
                foreach (var n in neighbors)
                {
                    Assert.IsFalse(board.IsMine(n),
                        $"First click neighbor {n} should not be a mine (seed={seed})");
                }
            }
        }

        [Test]
        public void MineCount_AtLeastRequested()
        {
            var click = new Coord3(3, 3, 3);
            var board = Generator.Generate(8, 25, click, 42);
            Assert.GreaterOrEqual(board.TotalMines(), 25,
                "Generator should place at least the requested number of mines");
        }

        [Test]
        public void Deterministic_SameSeed_SameBoard()
        {
            var click = new Coord3(2, 2, 2);
            var board1 = Generator.Generate(6, 10, click, 99);
            var board2 = Generator.Generate(6, 10, click, 99);

            for (int z = 0; z < 6; z++)
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                var c = new Coord3(x, y, z);
                Assert.AreEqual(board1.IsMine(c), board2.IsMine(c),
                    $"Mine state at {c} should match for same seed");
            }
        }

        [Test]
        public void DifferentSeeds_DifferentBoards()
        {
            // Use a larger grid with more mines so boost doesn't collapse seed differences
            var click = new Coord3(3, 3, 3);
            var board1 = Generator.Generate(8, 30, click, 1);
            var board2 = Generator.Generate(8, 30, click, 1000);

            bool anyDiff = false;
            for (int z = 0; z < 8 && !anyDiff; z++)
            for (int y = 0; y < 8 && !anyDiff; y++)
            for (int x = 0; x < 8 && !anyDiff; x++)
            {
                var c = new Coord3(x, y, z);
                if (board1.IsMine(c) != board2.IsMine(c))
                    anyDiff = true;
            }

            Assert.IsTrue(anyDiff, "Different seeds should (almost certainly) produce different boards");
        }

        [Test]
        public void Generate_DoesNotInstantWin_EasyDensity()
        {
            // 4^3 grid with 12 mines (18% density, matching Easy)
            var click = new Coord3(2, 2, 2);
            int size = 4;
            int mines = 12;

            for (int seed = 0; seed < 50; seed++)
            {
                var board = Generator.Generate(size, mines, click, seed);
                Assert.GreaterOrEqual(board.TotalMines(), mines,
                    $"Mine count should be >= requested (seed={seed})");

                // Simulate first click
                var simBoard = Generator.Generate(size, mines, click, seed);
                simBoard.Reveal(click);
                int revealed = simBoard.RevealedSafeCount;
                int totalSafe = size * size * size - simBoard.TotalMines();

                Assert.LessOrEqual(revealed, (int)(totalSafe * 0.40f),
                    $"First click revealed {revealed}/{totalSafe} safe cells — too many (seed={seed})");
            }
        }
    }
}
