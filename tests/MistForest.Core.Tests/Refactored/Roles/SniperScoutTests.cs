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
        public static void ScoutRow_FindsEnemyOnSelectedRow_AndEndsTurn()
        {
            Game game = MakeGame(GameMode.Scout.Instance, (1, 1), (3, 3));

            Assert.True(game.ExecuteAction(SniperScoutRow.New(callerUnitId: 0, row: 3)));

            ScoutRowEvent scoutEvent = Assert.IsType<ScoutRowEvent>(Assert.Single(game.Events));
            Assert.Equal(0, scoutEvent.UnitId);
            Assert.Equal(3, scoutEvent.Row);
            Assert.True(scoutEvent.Success);
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
    }
}
