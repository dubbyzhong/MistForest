using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SniperFlowTests
    {
        private static Game MakeSniperGame(
            int startX,
            int startY,
            int opponentX = 4,
            int opponentY = 4
        )
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(5, 5))));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(
                builder.SetPlayers(
                    Box.New(
                        new List<PlayerBuilder>
                        {
                            New(
                                roles: new List<RoleBuilder>
                                {
                                    SniperBuilder.New(
                                        new UnitBuilder[] { new SniperUnitBuilder() },
                                        board.At(startX, startY)!.Value
                                    )!,
                                }
                            )!,
                            New(
                                roles: new List<RoleBuilder>
                                {
                                    SniperBuilder.New(
                                        new UnitBuilder[] { new SniperUnitBuilder() },
                                        board.At(opponentX, opponentY)!.Value
                                    )!,
                                }
                            )!,
                        }
                    )
                )
            );
            return builder.Build()!;
        }

        [Fact]
        public static void ExecuteAction_SniperMove_UpdatesGridAndAppendsEvent()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node from = board.At(2, 2)!.Value;
            Board.Node to = board.At(2, 3)!.Value;

            Assert.Equal(new[] { 0 }, board.GetGrid(from)!.Units);

            game.ExecuteAction(GeneralMove.New(to));

            Assert.Empty(board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 0 }, board.GetGrid(to)!.Units);

            GeneralMoveEvent moveEvent = Assert.IsType<GeneralMoveEvent>(game.Events.Single());
            Assert.Equal(0, moveEvent.UnitId);
            Assert.Equal(new List<Board.Node> { from, to }, moveEvent.Path);
        }

        [Fact]
        public static void ExecuteAction_SniperSecondMove_InSameTurn_IsRejected()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node to = board.At(3, 2)!.Value;
            game.ExecuteAction(GeneralMove.New(to));
            Assert.Single(game.Events);
            Assert.Equal(1, game.CurrentPlayerId);

            Board.Node further = board.At(4, 2)!.Value;
            game.ExecuteAction(GeneralMove.New(further));
            Assert.Single(game.Events);
            Assert.Equal(new[] { 0 }, board.GetGrid(to)!.Units);
            Assert.Empty(board.GetGrid(further)!.Units);
        }

        [Fact]
        public static void ExecuteAction_SniperMove_OutOfRange_IsRejected()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node far = board.At(4, 4)!.Value;
            game.ExecuteAction(GeneralMove.New(far));
            Assert.Empty(game.Events);
            Assert.Equal(new[] { 0 }, board.GetGrid(board.At(2, 2)!.Value)!.Units);
        }

        [Fact]
        public static void SniperExecute_KillsTargetAndAppendsEvent()
        {
            Game game = MakeSniperGame(2, 2, opponentX: 3, opponentY: 3);
            Board board = game.Board;
            Board.Node target = board.At(3, 3)!.Value;

            Assert.Equal(new[] { 1 }, board.GetGrid(target)!.Units);
            game.ExecuteAction(SniperExecute.New(target));

            Assert.False(game.GetUnit(1)!.Alive);
            Assert.Empty(board.GetGrid(target)!.Units);
            Assert.True(((SniperUnit)game.GetUnit(0)!).ExecuteUsed);
            ExecuteEvent executeEvent = Assert.IsType<ExecuteEvent>(Assert.Single(game.Events));
            Assert.Equal(1, executeEvent.UnitId);
            Assert.Equal(0, executeEvent.ExecutorId);
        }

        [Fact]
        public static void ExecuteAction_GeneralEnd_AppendsEventAndAdvancesPlayer()
        {
            Game game = MakeSniperGame(2, 2);

            game.ExecuteAction(new GeneralEnd());

            GeneralEndEvent endEvent = Assert.IsType<GeneralEndEvent>(Assert.Single(game.Events));
            Assert.Equal(0, endEvent.UnitId);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void ExecuteAction_GeneralSurrender_KillsUnitsAndAdvancesPlayer()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node surrenderedUnitPosition = board.At(2, 2)!.Value;

            game.ExecuteAction(new GeneralSurrender());

            Assert.False(game.GetUnit(0)!.Alive);
            Assert.Empty(board.GetGrid(surrenderedUnitPosition)!.Units);
            GeneralSurrenderEvent surrenderEvent = Assert.IsType<GeneralSurrenderEvent>(
                Assert.Single(game.Events)
            );
            Assert.Equal(0, surrenderEvent.UnitId);
            Assert.Equal(1, game.CurrentPlayerId);
        }
    }
}
