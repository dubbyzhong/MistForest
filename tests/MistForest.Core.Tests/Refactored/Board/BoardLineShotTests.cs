using MistForest.Core.GameMap;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
    public class BoardLineShotTests
    {
        [Fact]
        public static void CanLineShot_SameNodeAndAlignedOpenPaths_AreClear()
        {
            Board board = Board.New(5, 5);
            Board.Node same = board.At(2, 2)!.Value;

            Assert.True(
                board.CanLineShot(same, same, diagonalBlocked: true, predicate: _ => true)
            );
            Assert.True(
                board.CanLineShot(
                    board.At(2, 1)!.Value,
                    board.At(2, 5)!.Value,
                    diagonalBlocked: true,
                    predicate: _ => true
                )
            );
            Assert.True(
                board.CanLineShot(
                    board.At(1, 3)!.Value,
                    board.At(5, 3)!.Value,
                    diagonalBlocked: true,
                    predicate: _ => true
                )
            );
        }

        [Fact]
        public static void CanLineShot_WallsOnAlignedPaths_BlockShot()
        {
            Board verticalWallBoard = Board.New(5, 5);
            Assert.True(verticalWallBoard.VerticalWalls.SetInner(0, 1ul << 4, out _));
            Assert.False(
                verticalWallBoard.CanLineShot(
                    verticalWallBoard.At(2, 1)!.Value,
                    verticalWallBoard.At(2, 5)!.Value,
                    diagonalBlocked: true,
                    predicate: _ => true
                )
            );

            Board horizontalWallBoard = Board.New(5, 5);
            Assert.True(horizontalWallBoard.HorizontalWalls.SetInner(0, 1ul << 1, out _));
            Assert.False(
                horizontalWallBoard.CanLineShot(
                    horizontalWallBoard.At(1, 2)!.Value,
                    horizontalWallBoard.At(5, 2)!.Value,
                    diagonalBlocked: true,
                    predicate: _ => true
                )
            );
        }

        [Fact]
        public static void CanLineShot_IntermediateWallTerrain_BlocksButTargetTerrainDoesNot()
        {
            Board board = Board.New(5, 5);
            Board.Node intermediate = board.At(2, 3)!.Value;
            Board.Node target = board.At(2, 5)!.Value;

            Assert.True(board.SetTerrain(intermediate, Terrain.Wall));
            Assert.False(
                board.CanLineShot(
                    board.At(2, 1)!.Value,
                    target,
                    diagonalBlocked: true,
                    predicate: node => board.GetGrid(node)!.Terrain is not Terrain.Wall
                )
            );

            Assert.True(board.SetTerrain(intermediate, Terrain.Ground));
            Assert.True(board.SetTerrain(target, Terrain.Wall));
            Assert.True(
                board.CanLineShot(
                    board.At(2, 1)!.Value,
                    target,
                    diagonalBlocked: true,
                    predicate: node => board.GetGrid(node)!.Terrain is not Terrain.Wall
                )
            );
        }

        [Fact]
        public static void CanLineShot_DiagonalCornerRule_UsesDiagonalBlockedFlag()
        {
            Board board = Board.New(5, 5);
            Board.Node from = board.At(1, 1)!.Value;
            Board.Node to = board.At(2, 2)!.Value;

            Assert.True(
                board.CanLineShot(from, to, diagonalBlocked: true, predicate: _ => true)
            );
            Assert.True(
                board.CanLineShot(from, to, diagonalBlocked: false, predicate: _ => true)
            );

            Assert.True(board.VerticalWalls.SetInner(0, 1ul, out _));
            Assert.True(
                board.CanLineShot(from, to, diagonalBlocked: true, predicate: _ => true)
            );
            Assert.False(
                board.CanLineShot(from, to, diagonalBlocked: false, predicate: _ => true)
            );

            Assert.True(board.HorizontalWalls.SetInner(0, 1ul, out _));
            Assert.False(
                board.CanLineShot(from, to, diagonalBlocked: true, predicate: _ => true)
            );
        }
    }
}
