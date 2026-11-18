using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    /// <summary>
    /// Rubble blocks rebuilding on its grid until it expires. Expiry is one full round after it appears
    /// and does not depend on whether the destroyer is still alive.
    /// </summary>
    public class RubbleExpiryTests
    {
        private static Game MakeGame(Board board)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
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
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(
                        new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(3, 4)!.Value
                    )!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(
                        new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(7, 7)!.Value
                    )!,
                })!,
            })));
            return builder.Build()!;
        }

        [Fact]
        public static void Rubble_ClearsAfterOneRound_EvenWhenDestroyerIsEliminated()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Game game = MakeGame(board);
            Board.Node fortPos = board.At(2, 3)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(4)!.SetPos(fortPos)!);
            Unit sniper = game.Players[0].Roles[0].GetUnits()[0];
            Unit wolf1 = game.Players[1].Roles[0].GetUnits()[0];
            Unit wolf2 = game.Players[2].Roles[0].GetUnits()[0];

            // p0 拆掉自家堡垒,废墟归属 p0,到期时间为"当前回合 + 玩家数"。
            Assert.True(game.ExecuteAction(GeneralDemolish.New(callerUnitId: sniper.Id, target: fortPos)));
            Building rubble = Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(0, rubble.Owner);
            Assert.Equal(rubble.Id, board.GetGrid(fortPos)!.GridBuilding);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(1, game.TurnCount);

            // p1 三连咬干掉 p0 唯一的单位:p0 出局,废墟的"摧毁者回合"再也不会出现。
            for (int i = 0; i < 3; ++i)
            {
                Assert.True(game.ExecuteAction(WolfBite.New(callerUnitId: wolf1.Id, target: sniper.Position)));
            }
            Assert.False(sniper.Alive);
            Assert.False(game.Players[0].Alive);
            Assert.True(
                board.GetGrid(fortPos)!.GridBuilding is int buildingId
                && game.Buildings[buildingId] is Building.Rubble
            );

            // 狼三下用完行动后回合自动结束,轮到 p2。
            Assert.Equal(2, game.CurrentPlayerId);
            Assert.Equal(2, game.TurnCount);
            Assert.True(
                board.GetGrid(fortPos)!.GridBuilding is int stillRubbleId
                && game.Buildings[stillRubbleId] is Building.Rubble
            );

            // p2 结束回合 → 一整轮过去(p0 已出局不会再有回合),废墟必须消失。
            Assert.True(game.ExecuteAction(GeneralEnd.New(wolf2.Id)));

            Assert.Null(board.GetGrid(fortPos)!.GridBuilding);
            Assert.True(board.GetGrid(fortPos)!.CanBuild());
        }

        [Fact]
        public static void ExpiredRubble_LeavesRebuiltFortInPlace()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Game game = MakeGame(board);
            Board.Node fortPos = board.At(2, 3)!.Value;
            Grid grid = board.GetGrid(fortPos)!;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(4)!.SetPos(fortPos)!);
            int sniperId = game.Players[0].Roles[0].GetUnits()[0].Id;
            int firstWolfId = game.Players[1].Roles[0].GetUnits()[0].Id;
            int secondWolfId = game.Players[2].Roles[0].GetUnits()[0].Id;

            Assert.True(game.ExecuteAction(GeneralDemolish.New(sniperId, fortPos)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(firstWolfId)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(secondWolfId)));
            Assert.Null(grid.GridBuilding);

            Assert.True(game.ExecuteAction(GeneralBuildFort.New(sniperId, fortPos)));
            Assert.Equal(1, game.CurrentPlayerId);
            Building.Fort rebuilt = Assert.IsType<Building.Fort>(game.Buildings[1]);
            Assert.Equal(rebuilt.Id, grid.GridBuilding);
            Assert.False(grid.CanBuild());
        }
    }
}
