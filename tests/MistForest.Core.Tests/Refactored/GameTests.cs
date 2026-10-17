using System.Collections.Generic;
using System.Linq;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored
{
    /// <summary>
    /// 测试用具体角色(Role 尚无完整实现)。
    /// </summary>
    public sealed class DummyRoleA : Role
    {
        public override Role Clone() => new DummyRoleA();
    }

    public sealed class DummyRoleB : Role
    {
        public override Role Clone() => new DummyRoleB();
    }

    public sealed class DummyRoleC : Role
    {
        public override Role Clone() => new DummyRoleC();
    }

    /// <summary>
    /// 测试用 RoleBuilder:不校验站位,可作用于任意棋盘。
    /// </summary>
    public sealed class DummyRoleBuilderA : RoleBuilder
    {
        public override bool CanInitialStay(Board board) => true;

        public override Role Build() => new DummyRoleA();
    }

    public sealed class DummyRoleBuilderB : RoleBuilder
    {
        public override bool CanInitialStay(Board board) => true;

        public override Role Build() => new DummyRoleB();
    }

    /// <summary>
    /// PlayerBuilder / RoleBuilder / Sniper 的单元测试。
    /// </summary>
    public class PlayerBuilderTests
    {
        private static Board.Node AnyNode() => Board.New(1, 1).At(1, 1)!;

        [Fact]
        public static void New_ValidRoleCounts()
        {
            Assert.NotNull(PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() }));
            Assert.NotNull(
                PlayerBuilder.New(
                    new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderB() }
                )
            );
        }

        [Fact]
        public static void New_TooManyRoles_Fails()
        {
            Assert.Null(
                PlayerBuilder.New(
                    new List<RoleBuilder>
                    {
                        new DummyRoleBuilderA(),
                        new DummyRoleBuilderB(),
                        RoleBuilder.SniperBuilder.New(AnyNode()),
                    }
                )
            );
        }

        [Fact]
        public static void New_DuplicateRoleTypes_Fails()
        {
            Assert.Null(
                PlayerBuilder.New(
                    new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderA() }
                )
            );
        }

        [Fact]
        public static void Build_ProducesPlayerWithBuiltRoles()
        {
            PlayerBuilder builder = PlayerBuilder.New(
                new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderB() }
            )!;
            Player player = builder.Build();
            Assert.IsType<DummyRoleA>(player.Roles[0]);
            Assert.IsType<DummyRoleB>(player.Roles[1]);
        }

        [Fact]
        public static void Player_Clone_DeepCopiesRoles()
        {
            Player player = Player.New(new Role[] { new DummyRoleA(), new DummyRoleB() });
            Player clone = player.Clone();
            Assert.NotSame(player, clone);
            Assert.NotSame(player.Roles[0], clone.Roles[0]);
            Assert.IsType<DummyRoleA>(clone.Roles[0]);
            Assert.IsType<DummyRoleB>(clone.Roles[1]);
        }

        [Fact]
        public static void SniperBuilder_Build_CreatesUnitWithHealthAndPosition()
        {
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Board.Node node = board.At(2, 3)!;
            Sniper sniper = (Sniper)RoleBuilder.SniperBuilder.New(node).Build();
            Assert.Equal(2, sniper.Unit.Health);
            Assert.Equal(node, sniper.Unit.Position);
        }

        [Fact]
        public static void Sniper_Clone_CopiesUnit()
        {
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Sniper sniper = Sniper.New(board.At(1, 1)!);
            Sniper clone = (Sniper)sniper.Clone();
            Assert.NotSame(sniper.Unit, clone.Unit);
            Assert.Equal(sniper.Unit.Health, clone.Unit.Health);
            Assert.Equal(sniper.Unit.Position, clone.Unit.Position);
        }

        [Fact]
        public static void SniperBuilder_CanInitialStay_FollowsTerrain()
        {
            Board heroes = Board.FromMode(GameMode.Heroes.Instance, out _)!;
            Assert.False(RoleBuilder.SniperBuilder.New(heroes.At(1, 1)!).CanInitialStay(heroes));
            Assert.True(RoleBuilder.SniperBuilder.New(heroes.At(1, 2)!).CanInitialStay(heroes));

            Board custom = Board.FromMode(GameMode.SelfDefine.New(3, 3), out _)!;
            custom.SetTerrain(custom.At(2, 2)!, Terrain.Lake);
            custom.SetTerrain(custom.At(3, 3)!, Terrain.Whirlpool);
            Assert.False(RoleBuilder.SniperBuilder.New(custom.At(2, 2)!).CanInitialStay(custom));
            Assert.True(RoleBuilder.SniperBuilder.New(custom.At(3, 3)!).CanInitialStay(custom));
        }
    }

    public class TeamTests
    {
        [Fact]
        public static void New_RequiresExactlyTwoPlayers()
        {
            PlayerBuilder a = PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;
            PlayerBuilder b = PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;
            PlayerBuilder c = PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;

            Assert.NotNull(Team.New(new List<PlayerBuilder> { a, b }));
            Assert.Null(Team.New(new List<PlayerBuilder> { a }));
            Assert.Null(Team.New(new List<PlayerBuilder> { a, b, c }));
        }
    }

    /// <summary>
    /// GameBuilder 的单元测试。
    /// </summary>
    public class GameBuilderTests
    {
        private static List<PlayerBuilder> MakePlayers(int count)
        {
            // 角色类型去重只约束单个玩家内部,不同玩家可以用同类型角色
            return Enumerable
                .Range(0, count)
                .Select(_ => PlayerBuilder.New(new List<RoleBuilder> { new DummyRoleBuilderA() })!)
                .ToList();
        }

        private static List<PlayerBuilder> MakeSnipers(Board board, int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(_ =>
                    PlayerBuilder.New(
                        new List<RoleBuilder> { RoleBuilder.SniperBuilder.New(board.At(1, 1)!) }
                    )!
                )
                .ToList();
        }

        [Fact]
        public static void Build_ScoutMode_SingleSniperPlayer_Succeeds()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Assert.NotNull(builder.SetPlayers(Box.New(MakeSnipers(board, 1))));
            Assert.NotNull(builder.Build());
        }

        [Fact]
        public static void SetPlayers_Scout_SecondPlayer_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Assert.Null(builder.SetPlayers(Box.New(MakeSnipers(board, 2))));
        }

        [Fact]
        public static void SetPlayers_Scout_NonSniperRole_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(1))));
        }

        [Fact]
        public static void SetPlayers_SniperOnWall_Fails()
        {
            // Heroes 默认图 (1,1) 是墙:CanInitialStay 应拒绝
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Board board = Board.FromMode(GameMode.Heroes.Instance, out _)!;
            List<PlayerBuilder> players = new()
            {
                PlayerBuilder.New(
                    new List<RoleBuilder> { RoleBuilder.SniperBuilder.New(board.At(1, 1)!) }
                )!,
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

        [Fact]
        public static void SetPlayers_InvalidCount_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(builder.SetPlayers(Box.New(new List<PlayerBuilder>())));
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

        [Fact]
        public static void SetTeams_ScoutMode_Fails()
        {
            // Scout 不允许组队
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            Assert.Null(builder.SetTeams(Box.New(teams)));
        }

        [Fact]
        public static void SetTeams_WithoutModeAndBoard_Fails()
        {
            GameBuilder builder = new();
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            Assert.Null(builder.SetTeams(Box.New(teams)));
        }

        [Fact]
        public static void Build_TeamGame_AssignsTeamIds()
        {
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.NotNull(builder.SetTeams(Box.New(teams)));
            Assert.NotNull(builder.Build());

            // SetTeams 副作用:队员获得队伍编号
            Assert.Equal(0, players[0].TeamId);
            Assert.Equal(0, players[1].TeamId);
            Assert.Equal(1, players[2].TeamId);
            Assert.Equal(1, players[3].TeamId);

            // 互斥:SetTeams 之后再 SetPlayers 失败
            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(2))));

            // 已带 TeamId 的玩家不能在别的 builder 里按单人局录入
            GameBuilder second = new();
            Assert.NotNull(second.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(second.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetTeams_InvalidTeamCount_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            List<PlayerBuilder> players = MakePlayers(2);
            Assert.Null(
                builder.SetTeams(
                    Box.New(
                        new List<Team>
                        {
                            Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                        }
                    )
                )
            );

            List<PlayerBuilder> six = MakePlayers(6);
            List<Team> threeTeams = Enumerable
                .Range(0, 3)
                .Select(i => Team.New(new List<PlayerBuilder> { six[i * 2], six[i * 2 + 1] })!)
                .ToList();
            GameBuilder second = new();
            Assert.NotNull(second.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.Null(second.SetTeams(Box.New(threeTeams)));
        }

        [Fact]
        public static void SetTeams_EmptyBox_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.Null(builder.SetTeams(Box<List<Team>>.Empty));
        }
    }
}
