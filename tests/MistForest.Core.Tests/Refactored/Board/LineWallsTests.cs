using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
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
    }
}
