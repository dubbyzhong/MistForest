using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Tests.Refactored.Fixtures;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using static MistForest.Core.Tests.Refactored.Fixtures.GameBuilderTestData;

namespace MistForest.Core.Tests.Refactored.GameSetup
{
    public class PlayerSetupTests
    {
        [Fact]
        public static void Build_ScoutMode_SniperDuel_Succeeds()
        {
            // Scout 为 1v1:两名玩家各带一名狙击手
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Assert.NotNull(builder.SetPlayers(Box.New(MakeSnipers(board, 2))));
            Assert.NotNull(builder.Build());
        }

        [Fact]
        public static void SetPlayers_Scout_NonSniperRole_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(1, 1)!.Value
                        )!,
                    }
                )!,
                New(roles: new List<RoleBuilder> { new DummyRoleBuilderB() })!,
            };
            Assert.Null(builder.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetPlayers_Scout_SecondRole_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(1, 1)!.Value
                        )!,
                        new DummyRoleBuilderB(),
                    }
                )!,
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(3, 3)!.Value
                        )!,
                        new DummyRoleBuilderA(),
                    }
                )!,
            };
            Assert.Null(builder.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetPlayers_SniperOnWall_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Board board = Board.FromMode(GameMode.Heroes.Instance, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(1, 1)!.Value
                        )!,
                    }
                )!,
                New(roles: new List<RoleBuilder> { new DummyRoleBuilderB() })!,
            };
            Assert.Null(builder.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetPlayers_WithoutModeAndBoard_Fails()
        {
            GameBuilder builder = new();
            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(2))));
        }

        [Fact]
        public static void SetPlayers_InvalidCount_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(builder.SetPlayers(Box.New(new List<PlayerBuilder>())));
            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(1)))); // 少于 2 人
            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(5))));
        }

        [Fact]
        public static void SetPlayers_MixedRoleCounts_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            List<PlayerBuilder> players = new()
            {
                PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() })!,
                PlayerBuilder.New(
                    new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderB() }
                )!,
            };
            Assert.Null(builder.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetPlayers_EmptyBox_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(builder.SetPlayers(Box<List<PlayerBuilder>>.Empty));
        }
    }
}
