using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SniperScoutTests
    {
        private static Game MakeGame(GameMode mode, params (int row, int col)[] positions)
        {
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(mode)));

            List<PlayerBuilder> players = positions
                .Select(position =>
                    New(
                        new List<PlayerBuilder.RoleBuilder>
                        {
                            SniperBuilder.New(
                                new UnitBuilder[] { new SniperUnitBuilder() },
                                board.At(position.row, position.col)!.Value
                            )!,
                        }
                    )!
                )
                .ToList();
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        [Fact]
        public static void ScoutRow_DoesNotRevealUndetectableUnit()
        {
            // Scout 类动作目前在非 Scout 模式下会被 Game.ExecuteAction 拦下,
            // 所以这里直接走角色层执行,只验证"成功判定"是否遵守免探测口径。
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<PlayerBuilder.RoleBuilder>
                {
                    SniperBuilder.New(
                        new UnitBuilder[] { new SniperUnitBuilder() },
                        board.At(1, 3)!.Value
                    )!,
                })!,
                New(new List<PlayerBuilder.RoleBuilder>
                {
                    HiddenWolfBuilder.New(
                        new UnitBuilder[] { new HiddenWolfUnitBuilder() },
                        board.At(5, 3)!.Value
                    )!,
                })!,
                New(new List<PlayerBuilder.RoleBuilder>
                {
                    WolfBuilder.New(
                        new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(6, 6)!.Value
                    )!,
                })!,
            })));
            Game game = builder.Build()!;
            Sniper sniper = Assert.IsType<Sniper>(game.Roles[0]);
            Assert.False(game.GetUnit(1)!.CanBeDetected);
            Assert.True(game.GetUnit(2)!.CanBeDetected);

            // 第 5 行只有人形隐狼:不应报成功。
            sniper.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 5), game);
            // 第 6 行有可见的座狼:应报成功。
            sniper.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 6), game);

            ScoutRowEvent[] events = game.Events.OfType<ScoutRowEvent>().ToArray();
            Assert.Equal(2, events.Length);
            Assert.False(events[0].Success);
            Assert.Equal(5, events[0].Row);
            Assert.True(events[1].Success);
            Assert.Equal(6, events[1].Row);
        }

        [Fact]
        public static void ScoutRow_FindsEnemyOnSelectedRow_AndEndsTurn()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));

            Assert.True(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 3)));

            ScoutRowEvent scoutEvent = Assert.IsType<ScoutRowEvent>(Assert.Single(game.Events));
            Assert.Equal(0, scoutEvent.UnitId);
            Assert.Equal(3, scoutEvent.Row);
            Assert.True(scoutEvent.Success);
            Assert.False(game.GetUnit(0)!.IsFirstAction);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void ScoutColumn_FindsEnemyOnSelectedColumn()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 2));

            Assert.True(game.ExecuteAction(SniperScoutCol.New(callerUnitId: 0, column: 2)));

            ScoutColEvent scoutEvent = Assert.IsType<ScoutColEvent>(Assert.Single(game.Events));
            Assert.Equal(0, scoutEvent.UnitId);
            Assert.Equal(2, scoutEvent.Col);
            Assert.True(scoutEvent.Success);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void ScoutRow_ReportsNoEnemyOnSelectedRow()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));

            Assert.True(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 2)));

            ScoutRowEvent scoutEvent = Assert.IsType<ScoutRowEvent>(Assert.Single(game.Events));
            Assert.Equal(2, scoutEvent.Row);
            Assert.False(scoutEvent.Success);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Scout_RejectsOutOfRangeRowAndColumnWithoutConsumingTurn()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));

            Assert.False(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 0)));
            Assert.False(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 4)));
            Assert.False(game.ExecuteAction(SniperScoutCol.New(callerUnitId: 0, column: 0)));
            Assert.False(game.ExecuteAction(SniperScoutCol.New(callerUnitId: 0, column: 4)));

            Assert.Empty(game.Events);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(SniperScoutCol.New(callerUnitId: 0, column: 3)));
            Assert.Single(game.Events);
        }

        [Fact]
        public static void ScoutAction_IsRejectedOutsideScoutMode()
        {
            Game game = MakeGame(GameMode.Heroes.Instance, (2, 2), (4, 4));

            Assert.False(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 2)));

            Assert.Empty(game.Events);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Scout_IsRejectedAfterAnotherActionHasStartedThisRound()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));
            Sniper role = Assert.IsType<Sniper>(game.Roles[0]);

            role.ExecuteAction(GeneralEnd.New(callerUnitId: 0), game);

            Assert.False(role.IsFirstAction);
            Assert.False(role.JudgeAction(SniperScoutRow.New(callerUnitId: 0, row: 2), game));
        }

        [Fact]
        public static void Scout_IsAvailableAgainAfterNewRoundBegins()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));

            Assert.True(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 3)));
            Assert.False(game.GetUnit(0)!.IsFirstAction);
            Assert.True(game.ExecuteAction(GeneralEnd.New(callerUnitId: 1)));

            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.GetUnit(0)!.IsFirstAction);
            Assert.True(game.ExecuteAction(SniperScoutCol.New(callerUnitId: 0, column: 3)));
            Assert.False(game.GetUnit(0)!.IsFirstAction);
        }
    }
}
