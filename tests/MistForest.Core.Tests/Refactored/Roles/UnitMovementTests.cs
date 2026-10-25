using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class UnitMovementTests
    {
        public static IEnumerable<object[]> RoleAndFortOwners()
        {
            foreach (string role in new[] { "Alpha", "Ink", "Sniper", "Wolf" })
            {
                foreach (int unitOwner in new[] { 0, 1 })
                {
                    foreach (int fortOwner in new[] { 0, 1 })
                    {
                        yield return new object[] { role, unitOwner, fortOwner };
                    }
                }
            }
        }

        public static IEnumerable<object[]> RoundCases()
        {
            foreach (object[] owners in RoleAndFortOwners())
            {
                foreach (int firstPlayerId in new[] { 0, 1 })
                {
                    yield return new object[] { owners[0], owners[1], owners[2], firstPlayerId };
                }
            }
        }

        private static Game MakeGame(string roleName, int unitOwner, int firstPlayerId)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node origin = board.At(3, 3)!.Value;
            RoleBuilder role = roleName switch
            {
                "Alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, origin)!,
                "Ink" => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, origin)!,
                "Sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, origin)!,
                "Wolf" => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, origin)!,
                _ => throw new ArgumentException("Unsupported role", nameof(roleName)),
            };
            RoleBuilder opponent = WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                board.At(7, 7)!.Value)!;
            List<PlayerBuilder> players = new();
            for (int playerId = 0; playerId < 2; ++playerId)
            {
                players.Add(New(new List<RoleBuilder> { playerId == unitOwner ? role : opponent })!);
            }
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        [Theory]
        [MemberData(nameof(RoleAndFortOwners))]
        public static void MoveView_UsesUnitOwnerWhenAnotherPlayerIsActing(
            string roleName, int unitOwner, int fortOwner)
        {
            int opponent = 1 - unitOwner;
            Game game = MakeGame(roleName, unitOwner, firstPlayerId: opponent);
            Unit unit = game.GetUnit(unitOwner)!;
            Board.Node origin = unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            bool canMove = fortOwner == unitOwner;
            game.AddBuilding(FortBuilder.New().SetOwner(fortOwner)!.SetHealth(3)!.SetPos(target)!);

            Assert.Equal(canMove, unit.GetView(game).CanMoveGrids.Any(entry => entry.node == target));
            Assert.Equal(opponent, game.CurrentPlayerId);
            Assert.False(game.ExecuteAction(GeneralMove.New(unit.Id, target)));
            Assert.Equal(origin, unit.Position);
            Assert.True(unit.IsFirstAction);
            Assert.Empty(game.Events);

            Assert.True(game.ExecuteAction(GeneralEnd.New(opponent)));
            Assert.Equal(unitOwner, game.CurrentPlayerId);
            Assert.Equal(canMove, unit.GetView(game).CanMoveGrids.Any(entry => entry.node == target));
            Assert.Equal(canMove, game.ExecuteAction(GeneralMove.New(unit.Id, target)));
            Assert.Equal(canMove ? target : origin, unit.Position);
            Assert.Equal(canMove, game.Board.GetGrid(target)!.Units.Contains(unit.Id));
            Assert.Equal(!canMove, game.Board.GetGrid(origin)!.Units.Contains(unit.Id));
        }

        [Theory]
        [MemberData(nameof(RoundCases))]
        public static void EndRound_WhirlpoolEntryUsesUnitOwnerRegardlessOfLastActingPlayer(
            string roleName, int unitOwner, int fortOwner, int firstPlayerId)
        {
            Game game = MakeGame(roleName, unitOwner, firstPlayerId);
            Unit unit = game.GetUnit(unitOwner)!;
            Board.Node origin = unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            bool canEnter = fortOwner == unitOwner;
            Assert.True(game.Board.SetTerrain(target, Terrain.Whirlpool));
            game.AddBuilding(FortBuilder.New().SetOwner(fortOwner)!.SetHealth(3)!.SetPos(target)!);

            Assert.True(game.ExecuteAction(GeneralEnd.New(firstPlayerId)));
            Assert.Equal(origin, unit.Position);
            Assert.Empty(game.Events.OfType<WhirlpoolAttachEvent>());
            Assert.True(game.ExecuteAction(GeneralEnd.New(1 - firstPlayerId)));

            Assert.Equal(canEnter ? target : origin, unit.Position);
            Assert.Equal(canEnter, game.Board.GetGrid(target)!.Units.Contains(unit.Id));
            Assert.Equal(!canEnter, game.Board.GetGrid(origin)!.Units.Contains(unit.Id));
            Assert.True(unit.IsFirstAction);
            Assert.False(unit.End);
            if (canEnter)
            {
                WhirlpoolAttachEvent attached = Assert.Single(game.Events.OfType<WhirlpoolAttachEvent>());
                Assert.Equal(unit.Id, attached.UnitId);
                Assert.Equal(new[] { origin, target }, attached.Path);
                Assert.Equal(MapConsts.WhirlpoolImmuneExpire, unit.WhirlpoolImmuneExpire);
            }
            else
            {
                Assert.Empty(game.Events.OfType<WhirlpoolAttachEvent>());
                Assert.Equal(0, unit.WhirlpoolImmuneExpire);
            }
        }
    }
}
