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
                    Assert.Equal(Terrain.Ground, board.GetGrid(board.At(x, y)!)!.OriginalBuilding);
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
                Assert.Equal(Terrain.Wall, board.GetGrid(board.At(x, y)!)!.OriginalBuilding);
            }
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(1, 2)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(3, 1)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Ground, board.GetGrid(board.At(5, 3)!)!.OriginalBuilding);
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
            Assert.Equal(Terrain.Lake, board.GetGrid(node)!.OriginalBuilding);
            Assert.True(board.SetTerrain(node, Terrain.Whirlpool));
            Assert.Equal(Terrain.Whirlpool, board.GetGrid(node)!.OriginalBuilding);
            Assert.True(board.SetTerrain(node, Terrain.Ground));
            Assert.Equal(Terrain.Ground, board.GetGrid(node)!.OriginalBuilding);
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
                        board.GetGrid(board.At(x, y)!)!.OriginalBuilding,
                        decoded.GetGrid(decoded.At(x, y)!)!.OriginalBuilding
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

            Box<byte[]> code = Box.New(board.IntoCode());
            Board decoded = Board.FromCode(code, out string? error)!;
            Assert.NotNull(decoded);
            Assert.Null(error);
            Assert.Equal(Terrain.Lake, decoded.GetGrid(decoded.At(2, 3)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Whirlpool, decoded.GetGrid(decoded.At(4, 4)!)!.OriginalBuilding);
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
            board.SetTerrain(board.At(3, 2)!, Terrain.Lake);
            board.SetTerrain(board.At(6, 4)!, Terrain.Whirlpool);
            board.HorizontalWalls.SetInner(0, 0x1, out _);
            board.VerticalWalls.SetInner(0, 0x2, out _);

            Board decoded = Board.FromCode(Box.New(board.IntoCode()), out string? error)!;
            Assert.NotNull(decoded);
            Assert.Null(error);
            Assert.Equal(7, decoded.Height);
            Assert.Equal(4, decoded.Width);
            Assert.Equal(24, decoded.HorizontalWalls.Capacity);
            Assert.Equal(21, decoded.VerticalWalls.Capacity);
            Assert.Equal(Terrain.Lake, decoded.GetGrid(decoded.At(3, 2)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Whirlpool, decoded.GetGrid(decoded.At(6, 4)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Ground, decoded.GetGrid(decoded.At(2, 2)!)!.OriginalBuilding);
            Assert.True(decoded.HorizontalWalls[0]);
            Assert.False(decoded.HorizontalWalls[1]);
            Assert.True(decoded.VerticalWalls[1]);
            Assert.False(decoded.VerticalWalls[0]);
        }

        [Fact]
        public static void SetTerrain_InvalidTerrain_ReturnsFalse()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            Board.Node node = board.At(2, 2)!;
            Assert.False(board.SetTerrain(node, (Terrain)99));
            Assert.Equal(Terrain.Ground, board.GetGrid(node)!.OriginalBuilding);
        }

        [Fact]
        public static void Clone_IsDeep()
        {
            Board board = Board.FromMode(new GameMode.Heroes(), out _)!;
            board.SetTerrain(board.At(2, 2)!, Terrain.Lake);
            board.HorizontalWalls.SetInner(0, 0x8, out _);

            Board clone = board.Clone();
            clone.SetTerrain(clone.At(2, 2)!, Terrain.Whirlpool);
            clone.HorizontalWalls.SetInner(0, 0x1, out _);

            Assert.Equal(Terrain.Lake, board.GetGrid(board.At(2, 2)!)!.OriginalBuilding);
            Assert.Equal(Terrain.Whirlpool, clone.GetGrid(clone.At(2, 2)!)!.OriginalBuilding);
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

    /// <summary>
    /// Grid 的单元测试。
    /// </summary>
    public class GridTests
    {
        [Fact]
        public static void New_DefaultsToGround()
        {
            Assert.Equal(Terrain.Ground, Grid.New().OriginalBuilding);
        }

        [Fact]
        public static void WithOriginalBuilding_SetsAndReturnsSelf()
        {
            Grid grid = Grid.New();
            Assert.Same(grid, grid.WithOriginalBuilding(Terrain.Lake));
            Assert.Equal(Terrain.Lake, grid.OriginalBuilding);
        }

        [Fact]
        public static void Clone_IsIndependent()
        {
            Grid grid = Grid.New().WithOriginalBuilding(Terrain.Wall);
            Grid clone = grid.Clone();
            clone.OriginalBuilding = Terrain.Lake;
            Assert.NotSame(grid, clone);
            Assert.Equal(Terrain.Wall, grid.OriginalBuilding);
            Assert.Equal(Terrain.Lake, clone.OriginalBuilding);
        }
    }

    /// <summary>
    /// LineWalls 的单元测试。
    /// </summary>
    public class LineWallsTests
    {
        [Fact]
        public static void WithCapacity_SetsCapacity_AllFalse()
        {
            LineWalls walls = LineWalls.WithCapacity(13);
            Assert.Equal(13, walls.Capacity);
            for (int i = 0; i < 13; ++i)
            {
                Assert.False(walls[i]);
            }
        }

        [Fact]
        public static void Indexer_OutOfRange_Throws()
        {
            LineWalls walls = LineWalls.WithCapacity(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = walls[10]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = walls[-1]);
        }

        [Fact]
        public static void SetInner_WordVisibleThroughIndexer()
        {
            LineWalls walls = LineWalls.WithCapacity(20);
            Assert.True(walls.SetInner(0, 0x8, out string? error));
            Assert.Null(error);
            Assert.True(walls[3]);
            Assert.False(walls[0]);
        }

        [Fact]
        public static void SetInner_InvalidWordIndex_Fails()
        {
            LineWalls walls = LineWalls.WithCapacity(20); // 20 位 → 1 个 word:索引 0
            Assert.False(walls.SetInner(3, 1, out string? error));
            Assert.NotNull(error);
            Assert.False(walls.SetInner(-1, 1, out error));
            Assert.NotNull(error);
        }

        [Fact]
        public static void Clone_IsIndependent()
        {
            LineWalls walls = LineWalls.WithCapacity(20);
            walls.SetInner(0, 0x8, out _);
            LineWalls clone = walls.Clone();
            clone.SetInner(0, 0x1, out _);
            Assert.True(walls[3]);
            Assert.False(walls[0]);
            Assert.True(clone[0]);
            Assert.False(clone[3]);
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
