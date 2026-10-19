using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
    public class BoardFactoryTests
    {
        [Fact]
        public static void FromMode_Scout_3x3_AllGround()
        {
            Board board = Board.FromMode(new GameMode.Scout(), out string? error)!;
            Assert.NotNull(board);
            Assert.Null(error);
            Assert.Equal(ModeConsts.ScoutLength, board.Width);
            Assert.Equal(ModeConsts.ScoutLength, board.Height);
            for (int x = 1; x <= ModeConsts.ScoutLength; ++x)
            {
                for (int y = 1; y <= ModeConsts.ScoutLength; ++y)
                {
                    Assert.Equal(Terrain.Ground, board.GetGrid(board.At(x, y)!.Value)!.Terrain);
                }
            }
        }

        [Fact]
        public static void FromMode_Heroes_5x5_CrossWalls()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Assert.NotNull(board);
            Assert.Equal(ModeConsts.HeroesLen, board.Width);
            Assert.Equal(ModeConsts.HeroesLen, board.Height);
            foreach (var (x, y) in ModeConsts.HeroesWallPos)
            {
                Assert.Equal(Terrain.Wall, board.GetGrid(board.At(x, y)!.Value)!.Terrain);
            }
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(1, 2)!.Value)!.Terrain);
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(3, 1)!.Value)!.Terrain);
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(5, 3)!.Value)!.Terrain);
        }

        [Fact]
        public static void FromMode_SelfDefine_CustomSize()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            Assert.NotNull(board);
            Assert.Equal(7, board.Height);
            Assert.Equal(4, board.Width);
        }

        [Fact]
        public static void SetTerrain_RoundTrip()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node node = board.At(2, 2)!.Value;

            Assert.True(board.SetTerrain(node, Terrain.Lake));
            Assert.Equal(Terrain.Lake, board.GetGrid(node)!.Terrain);
            Assert.True(board.SetTerrain(node, Terrain.Whirlpool));
            Assert.Equal(Terrain.Whirlpool, board.GetGrid(node)!.Terrain);
            Assert.True(board.SetTerrain(node, Terrain.Ground));
            Assert.Equal(Terrain.Ground, board.GetGrid(node)!.Terrain);
        }

        [Fact]
        public static void At_ValidAndInvalid()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Assert.NotNull(board.At(1, 1));
            Assert.NotNull(board.At(ModeConsts.HeroesLen, ModeConsts.HeroesLen));
            Assert.NotNull(board.At(3, 3));
            Assert.Null(board.At(0, 1));
            Assert.Null(board.At(ModeConsts.HeroesLen + 1, 1));
            Assert.Null(board.At(1, 0));
            Assert.Null(board.At(1, ModeConsts.HeroesLen + 1));
            Assert.Null(board.At(-1, -1));
        }

        [Fact]
        public static void RowOf_ColOf_Inverse()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            for (int x = 1; x <= ModeConsts.HeroesLen; ++x)
            {
                for (int y = 1; y <= ModeConsts.HeroesLen; ++y)
                {
                    Board.Node node = board.At(x, y)!.Value;
                    Assert.Equal(x, board.RowOf(node));
                    Assert.Equal(y, board.ColOf(node));
                }
            }
        }

        [Fact]
        public static void Distance_Manhattan()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node center = board.At(3, 3)!.Value;
            Assert.Equal(0, board.Distance(center, board.At(3, 3)!.Value));
            Assert.Equal(1, board.Distance(center, board.At(3, 4)!.Value));
            Assert.Equal(1, board.Distance(center, board.At(4, 3)!.Value));
            Assert.Equal(2, board.Distance(center, board.At(3, 5)!.Value));
            Assert.Equal(4, board.Distance(center, board.At(1, 1)!.Value));
            Assert.Equal(4, board.Distance(center, board.At(5, 5)!.Value));
            Assert.Equal(
                board.Distance(center, board.At(1, 1)!.Value),
                board.Distance(board.At(1, 1)!.Value, center)
            );
        }

        [Fact]
        public static void Move_FourDirections_AndOutOfBounds()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            Board.Node node = board.At(2, 3)!.Value;
            Assert.Equal(board.At(3, 3)!.Value, board.Move(node, 1, 0));
            Assert.Equal(board.At(1, 3)!.Value, board.Move(node, -1, 0));
            Assert.Equal(board.At(2, 4)!.Value, board.Move(node, 0, 1));
            Assert.Equal(board.At(2, 2)!.Value, board.Move(node, 0, -1));
            Assert.Null(board.Move(board.At(1, 1)!.Value, -1, 0));
            Assert.Null(board.Move(board.At(1, 1)!.Value, 0, -1));
            Assert.Null(board.Move(board.At(5, 5)!.Value, 1, 0));
            Assert.Null(board.Move(board.At(5, 5)!.Value, 0, 1));
        }
    }
}
