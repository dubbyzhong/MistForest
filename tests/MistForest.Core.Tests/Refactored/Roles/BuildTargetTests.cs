using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    /// <summary>
    /// Build targets must be in build range and reachable by line of sight. A grid occupied by a hidden
    /// unit may still be selected: executing then fails but still consumes the turn. Grids occupied by
    /// detectable units are never legal targets.
    /// </summary>
    public class BuildTargetTests
    {
        private static Game MakeGame(Func<Board, RoleBuilder> opponentAt)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    SniperBuilder.New(
                        new UnitBuilder[] { new SniperUnitBuilder() },
                        board.At(3, 3)!.Value
                    )!,
                })!,
                New(new List<RoleBuilder> { opponentAt(board) })!,
            })));
            return builder.Build()!;
        }

        [Fact]
        public static void FortAndWallTargets_ExcludeGridOccupiedByDetectableUnit()
        {
            Game game = MakeGame(board =>
                WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 4)!.Value)!
            );
            Unit sniper = game.GetUnit(0)!;
            Unit wolf = game.GetUnit(1)!;
            Assert.True(wolf.CanBeDetected);

            Board.Node occupied = game.Board.At(3, 4)!.Value;
            Board.Node free = game.Board.At(3, 2)!.Value;
            UnitView view = sniper.GetView(game);

            Assert.DoesNotContain(occupied, view.CanBuildFortGrids);
            Assert.DoesNotContain(occupied, view.CanBuildWallGrids);
            Assert.Contains(free, view.CanBuildFortGrids);
            Assert.Contains(free, view.CanBuildWallGrids);

            Assert.False(game.ExecuteAction(GeneralBuildFort.New(sniper.Id, occupied)));
            Assert.False(
                game.ExecuteAction(
                    GeneralBuildWall.New(sniper.Id, new[] { occupied, game.Board.At(2, 4)!.Value })!
                )
            );
            Assert.Empty(game.Buildings);
            Assert.True(sniper.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void FortTarget_OccupiedByUndetectableUnit_IsSelectableButFailsOnExecution()
        {
            Game game = MakeGame(board =>
                HiddenWolfBuilder.New(
                    new UnitBuilder[] { new HiddenWolfUnitBuilder() },
                    board.At(3, 4)!.Value
                )!
            );
            Unit sniper = game.GetUnit(0)!;
            Unit hiddenWolf = game.GetUnit(1)!;
            Assert.False(hiddenWolf.CanBeDetected);

            Board.Node occupied = game.Board.At(3, 4)!.Value;
            Assert.Contains(occupied, sniper.GetView(game).CanBuildFortGrids);
            Assert.True(game.ExecuteAction(GeneralBuildFort.New(sniper.Id, occupied)));

            GeneralBuildFortEvent buildEvent = Assert.IsType<GeneralBuildFortEvent>(
                Assert.Single(game.Events)
            );
            Assert.False(buildEvent.Success);
            Assert.Empty(game.Buildings);
            Assert.Equal(1, game.CurrentPlayerId);
        }
    }
}
