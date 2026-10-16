using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored
{
    public class BoardTests
    {
        // ===== FromMode =====

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
                    Assert.Equal(Terrain.Ground, board.GetTerrain(board.At(x, y)!));
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
                Assert.Equal(Terrain.Wall, board.GetTerrain(board.At(x, y)!));
            }
            Assert.Equal(Terrain.Ground, board.GetTerrain(board.At(1, 2)!));
            Assert.Equal(Terrain.Ground, board.GetTerrain(board.At(3, 1)!));
            Assert.Equal(Terrain.Ground, board.GetTerrain(board.At(5, 3)!));
        }

        [Fact]
        public static void FromMode_SelfDefine_CustomSize()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            Assert.NotNull(board);
            Assert.Equal(7, board.Height);
            Assert.Equal(4, board.Width);
        }

        // ===== GetTerrain / SetTerrain =====

        [Fact]
        public static void SetTerrain_RoundTrip()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node node = board.At(2, 2)!;

            Assert.True(board.SetTerrain(node, Terrain.Lake));
            Assert.Equal(Terrain.Lake, board.GetTerrain(node));
            Assert.True(board.SetTerrain(node, Terrain.Whirlpool));
            Assert.Equal(Terrain.Whirlpool, board.GetTerrain(node));
            Assert.True(board.SetTerrain(node, Terrain.Ground));
            Assert.Equal(Terrain.Ground, board.GetTerrain(node));
        }

        // ===== At / RowOf / ColOf =====

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
                    Board.Node node = board.At(x, y)!;
                    Assert.Equal(x, board.RowOf(node));
                    Assert.Equal(y, board.ColOf(node));
                }
            }
        }

        // ===== Distance =====

        [Fact]
        public static void Distance_Manhattan()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node center = board.At(3, 3)!;
            Assert.Equal(0, board.Distance(center, board.At(3, 3)!));
            Assert.Equal(1, board.Distance(center, board.At(3, 4)!));
            Assert.Equal(1, board.Distance(center, board.At(4, 3)!));
            Assert.Equal(2, board.Distance(center, board.At(3, 5)!));
            Assert.Equal(4, board.Distance(center, board.At(1, 1)!));
            Assert.Equal(4, board.Distance(center, board.At(5, 5)!));
            Assert.Equal(
                board.Distance(center, board.At(1, 1)!),
                board.Distance(board.At(1, 1)!, center)
            );
        }

        // ===== LineWalls =====

        [Fact]
        public static void LineWalls_CapacityFormula()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Assert.Equal(20, board.HorizontalWalls.Capacity);
            Assert.Equal(20, board.VerticalWalls.Capacity);

            Board scout = Board.FromMode(new GameMode.Scout(), out _)!;
            Assert.Equal(6, scout.HorizontalWalls.Capacity);
            Assert.Equal(6, scout.VerticalWalls.Capacity);
        }

        [Fact]
        public static void LineWalls_Indexer_Reads()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            for (int i = 0; i < board.HorizontalWalls.Capacity; ++i)
            {
                Assert.False(board.HorizontalWalls[i]);
            }
            board.HorizontalWalls.SetInner(0, 0x8, out _);
            Assert.True(board.HorizontalWalls[3]);
            Assert.False(board.HorizontalWalls[0]);
            Assert.False(board.HorizontalWalls[2]);
            Assert.False(board.HorizontalWalls[4]);
        }

        // ===== IntoCode / FromCode =====

        [Fact]
        public static void IntoCode_FromCode_RoundTrip()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            byte[] code = board.IntoCode();
            Assert.NotEmpty(code);

            Board decoded = Board.FromCode(code, out _)!;
            Assert.NotNull(decoded);
            Assert.Equal(board.Width, decoded.Width);
            Assert.Equal(board.Height, decoded.Height);

            for (int x = 1; x <= ModeConsts.HeroesLen; ++x)
            {
                for (int y = 1; y <= ModeConsts.HeroesLen; ++y)
                {
                    Assert.Equal(
                        board.GetTerrain(board.At(x, y)!),
                        decoded.GetTerrain(decoded.At(x, y)!)
                    );
                }
            }
            Assert.Equal(board.HorizontalWalls.Inner, decoded.HorizontalWalls.Inner);
            Assert.Equal(board.VerticalWalls.Inner, decoded.VerticalWalls.Inner);
        }

        [Fact]
        public static void RoundTrip_WithCustomTerrainAndWalls()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            board.SetTerrain(board.At(2, 3)!, Terrain.Lake);
            board.SetTerrain(board.At(4, 4)!, Terrain.Whirlpool);
            board.HorizontalWalls.SetInner(0, 0x0000_0000_0000_0008, out _);
            board.VerticalWalls.SetInner(0, 0x0000_0000_0000_0001, out _);

            byte[] code = board.IntoCode();
            Board decoded = Board.FromCode(code, out string? error)!;
            Assert.NotNull(decoded);
            Assert.Null(error);
            Assert.Equal(Terrain.Lake, decoded.GetTerrain(decoded.At(2, 3)!));
            Assert.Equal(Terrain.Whirlpool, decoded.GetTerrain(decoded.At(4, 4)!));
            Assert.True(decoded.HorizontalWalls[3]);
            Assert.True(decoded.VerticalWalls[0]);
            Assert.False(decoded.HorizontalWalls[0]);
            Assert.False(decoded.VerticalWalls[5]);
        }

        [Fact]
        public static void FromCode_EmptyBytes_Fails()
        {
            Board? board = Board.FromCode(Array.Empty<byte>(), out string? error);
            Assert.Null(board);
            Assert.NotNull(error);
        }

        [Fact]
        public static void FromCode_Truncated_Fails()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            byte[] full = board.IntoCode();
            Board? result = Board.FromCode(full[..(full.Length / 2)], out string? error);
            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public static void FromCode_InvalidTerrain_Fails()
        {
            byte[] code = new byte[8 + 25];
            ByteWriter writer = ByteWriter.New(code);
            writer.WriteInt(5, out _);
            writer.WriteInt(5, out _);
            for (int i = 0; i < 25; ++i)
            {
                writer.WriteByte(i == 0 ? (byte)99 : (byte)Terrain.Ground, out _);
            }
            Board? result = Board.FromCode(code, out string? error);
            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Contains("invalid terrain", error);
        }

        [Fact]
        public static void IntoCode_SizeFormula()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Assert.Equal(81, board.IntoCode().Length);
            Board scout = Board.FromMode(new GameMode.Scout(), out _)!;
            Assert.Equal(33, scout.IntoCode().Length);
        }
    }

    public class GameModeTests
    {
        [Fact]
        public static void PatternMatch_AllThreeVariants()
        {
            GameMode scout = new GameMode.Scout();

            string name = scout switch
            {
                GameMode.Scout => "scout",
                GameMode.Heroes => "heroes",
                GameMode.SelfDefine => "custom",
                _ => "unknown",
            };
            Assert.Equal("scout", name);
        }

        [Fact]
        public static void SelfDefine_Factory_SetsDimensions()
        {
            GameMode.SelfDefine mode = GameMode.SelfDefine.New(7, 4);
            Assert.Equal(7, mode.Height);
            Assert.Equal(4, mode.Width);
        }

        [Fact]
        public static void SelfDefine_FromMode_ProducesCustomBoard()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(4, 7), out _)!;
            Assert.NotNull(board);
            Assert.Equal(4, board.Height);
            Assert.Equal(7, board.Width);
        }

        [Fact]
        public static void Record_Equality()
        {
            Assert.Equal(new GameMode.Scout(), new GameMode.Scout());
            Assert.Equal(new GameMode.Heroes(), new GameMode.Heroes());
            Assert.NotEqual(new GameMode.Scout(), (GameMode)new GameMode.Heroes());
            Assert.Equal(GameMode.SelfDefine.New(3, 3), GameMode.SelfDefine.New(3, 3));
            Assert.NotEqual(GameMode.SelfDefine.New(3, 3), GameMode.SelfDefine.New(3, 4));
        }
    }
}
