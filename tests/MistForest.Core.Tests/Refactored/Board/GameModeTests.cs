using MistForest.Core.GameMap;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
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
