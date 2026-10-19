using System.Collections.Generic;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.Tests.Refactored.Fixtures.GameBuilderTestData;

namespace MistForest.Core.Tests.Refactored.GameSetup
{
    public class ModeSetupTests
    {
        [Fact]
        public static void Build_SelfDefineMode_CustomBoard_Succeeds()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(7, 4))));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(MakePlayers(2))));
            Assert.NotNull(builder.Build());
        }

        [Fact]
        public static void Build_MissingSteps_Fails()
        {
            Assert.Null(new GameBuilder().Build());

            GameBuilder modeOnly = new();
            Assert.NotNull(modeOnly.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(modeOnly.Build());

            GameBuilder noPlayers = new();
            Assert.NotNull(noPlayers.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(7, 4))));
            Assert.NotNull(
                noPlayers.SetBoard(Box.New(Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!))
            );
            Assert.Null(noPlayers.Build());
        }

        [Fact]
        public static void SetMode_SecondCall_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.Null(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
        }

        [Fact]
        public static void SetMode_EmptyBox_Fails()
        {
            GameBuilder builder = new();
            Assert.Null(builder.SetMode(Box<GameMode>.Empty));
        }

        [Fact]
        public static void SetBoard_AfterPresetMode_Fails()
        {
            // Scout/Heroes 在 SetMode 时已生成棋盘,再 SetBoard 应失败
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            Assert.Null(builder.SetBoard(Box.New(board)));
        }

        [Fact]
        public static void SetBoard_WithoutMode_Fails()
        {
            GameBuilder builder = new();
            Board board = Board.FromMode(GameMode.SelfDefine.New(7, 4), out _)!;
            Assert.Null(builder.SetBoard(Box.New(board)));
        }

        [Fact]
        public static void SetBoard_EmptyBox_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(7, 4))));
            Assert.Null(builder.SetBoard(Box<Board>.Empty));
        }
    }
}
