using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
    public class BoardSerializationTests
    {
        [Fact]
        public static void IntoCode_FromCode_RoundTrip()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Box<byte[]> code = Box.New(board.IntoCode());
            Assert.NotNull(code);

            Board decoded = Board.FromCode(code, out _)!;
            Assert.NotNull(decoded);
            Assert.Equal(board.Width, decoded.Width);
            Assert.Equal(board.Height, decoded.Height);

            for (int x = 1; x <= ModeConsts.HeroesLen; ++x)
            {
                for (int y = 1; y <= ModeConsts.HeroesLen; ++y)
                {
                    Assert.Equal(
                        board.GetGrid(board.At(x, y)!.Value)!.Terrain,
                        decoded.GetGrid(decoded.At(x, y)!.Value)!.Terrain
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
            board.SetTerrain(board.At(2, 3)!.Value, Terrain.Lake);
            board.SetTerrain(board.At(4, 4)!.Value, Terrain.Whirlpool);
            board.HorizontalWalls.SetInner(0, 0x0000_0000_0000_0008, out _);
            board.VerticalWalls.SetInner(0, 0x0000_0000_0000_0001, out _);

            Box<byte[]> code = Box.New(board.IntoCode());
            Board decoded = Board.FromCode(code, out string? error)!;
            Assert.NotNull(decoded);
            Assert.Null(error);
            Assert.Equal(Terrain.Lake, decoded.GetGrid(decoded.At(2, 3)!.Value)!.Terrain);
            Assert.Equal(Terrain.Whirlpool, decoded.GetGrid(decoded.At(4, 4)!.Value)!.Terrain);
            Assert.True(decoded.HorizontalWalls[3]);
            Assert.True(decoded.VerticalWalls[0]);
            Assert.False(decoded.HorizontalWalls[0]);
            Assert.False(decoded.VerticalWalls[5]);
        }

        [Fact]
        public static void FromCode_EmptyBytes_Fails()
        {
            Board? board = Board.FromCode(Box.New(Array.Empty<byte>()), out string? error);
            Assert.Null(board);
            Assert.NotNull(error);
        }

        [Fact]
        public static void FromCode_Truncated_Fails()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Box<byte[]> full = Box.New(board.IntoCode());
            Board? result = Board.FromCode(
                full.Map(full => full[..(full.Length / 2)]),
                out string? error
            );
            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public static void FromCode_InvalidTerrain_Fails()
        {
            byte[]? code = new byte[8 + 25];
            ByteWriter writer = ByteWriter.New(ref code);
            writer.WriteInt(ModeConsts.HeroesLen, out _);
            writer.WriteInt(ModeConsts.HeroesLen, out _);
            for (int i = 0; i < ModeConsts.HeroesLen * ModeConsts.HeroesLen; ++i)
            {
                writer.WriteByte(i == 0 ? (byte)99 : (byte)Terrain.Ground, out _);
            }
            Board? result = Board.FromCode(Box.From(ref code), out string? error);
            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Contains("invalid terrain", error);
        }

        [Fact]
        public static void IntoCode_SizeFormula()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Assert.Equal(49, board.IntoCode().Length);
            Board scout = Board.FromMode(new GameMode.Scout(), out _)!;
            Assert.Equal(33, scout.IntoCode().Length);
        }

        [Fact]
        public static void IntoCode_FromCode_NonSquare_RoundTrip()
        {
            // 7 行 4 列:宽高与横竖墙容量均不同,可抓住序列化字段顺序颠倒的错误
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            board.SetTerrain(board.At(3, 2)!.Value, Terrain.Lake);
            board.SetTerrain(board.At(6, 4)!.Value, Terrain.Whirlpool);
            board.HorizontalWalls.SetInner(0, 0x1, out _);
            board.VerticalWalls.SetInner(0, 0x2, out _);

            Board decoded = Board.FromCode(Box.New(board.IntoCode()), out string? error)!;
            Assert.NotNull(decoded);
            Assert.Null(error);
            Assert.Equal(7, decoded.Height);
            Assert.Equal(4, decoded.Width);
            Assert.Equal(24, decoded.HorizontalWalls.Capacity);
            Assert.Equal(21, decoded.VerticalWalls.Capacity);
            Assert.Equal(Terrain.Lake, decoded.GetGrid(decoded.At(3, 2)!.Value)!.Terrain);
            Assert.Equal(Terrain.Whirlpool, decoded.GetGrid(decoded.At(6, 4)!.Value)!.Terrain);
            Assert.Equal(Terrain.Ground, decoded.GetGrid(decoded.At(2, 2)!.Value)!.Terrain);
            Assert.True(decoded.HorizontalWalls[0]);
            Assert.False(decoded.HorizontalWalls[1]);
            Assert.True(decoded.VerticalWalls[1]);
            Assert.False(decoded.VerticalWalls[0]);
        }

        [Fact]
        public static void SetTerrain_InvalidTerrain_ReturnsFalse()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node node = board.At(2, 2)!.Value;
            Assert.False(board.SetTerrain(node, (Terrain)99));
            Assert.Equal(Terrain.Ground, board.GetGrid(node)!.Terrain);
        }

        [Fact]
        public static void Clone_IsDeep()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            board.SetTerrain(board.At(2, 2)!.Value, Terrain.Lake);
            board.HorizontalWalls.SetInner(0, 0x8, out _);

            Board clone = board.Clone();
            clone.SetTerrain(clone.At(2, 2)!.Value, Terrain.Whirlpool);
            clone.HorizontalWalls.SetInner(0, 0x1, out _);

            Assert.Equal(Terrain.Lake, board.GetGrid(board.At(2, 2)!.Value)!.Terrain);
            Assert.Equal(Terrain.Whirlpool, clone.GetGrid(clone.At(2, 2)!.Value)!.Terrain);
            Assert.True(board.HorizontalWalls[3]);
            Assert.False(board.HorizontalWalls[0]);
            Assert.True(clone.HorizontalWalls[0]);
            Assert.False(clone.HorizontalWalls[3]);
        }

        [Fact]
        public static void FromCode_EmptyBox_Fails()
        {
            Board? board = Board.FromCode(Box<byte[]>.Empty, out string? error);
            Assert.Null(board);
            Assert.NotNull(error);
        }
    }
}
