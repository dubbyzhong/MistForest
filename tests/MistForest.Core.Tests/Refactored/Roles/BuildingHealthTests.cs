using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class BuildingHealthTests
    {
        public static IEnumerable<object[]> ConstructionCases()
        {
            foreach (string role in new[] { "Alpha", "Armiger", "Ink", "Sniper", "Wolf" })
            foreach (int wallSegments in new[] { 0, 1, 2 })
                yield return new object[] { role, wallSegments };
        }

        [Theory]
        [MemberData(nameof(ConstructionCases))]
        public static void GeneralConstruction_CreatesBuildingsWithOneDisplayedHealthPoint(
            string roleName, int wallSegments)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node start = board.At(3, 3)!.Value;
            RoleBuilder role = roleName switch
            {
                "Alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, start)!,
                "Armiger" => ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, start)!,
                "Ink" => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, start)!,
                "Sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, start)!,
                "Wolf" => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, start)!,
                _ => throw new ArgumentException("Unsupported role", nameof(roleName)),
            };
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder> { role })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            })));
            Game game = builder.Build()!;
            Board.Node first = board.At(2, 3)!.Value;
            Board.Node[] targets = wallSegments == 2
                ? new[] { first, board.At(2, 4)!.Value }
                : new[] { first };
            MistForest.Core.GameRefactored.Action action = wallSegments == 0
                ? GeneralBuildFort.New(0, first)
                : GeneralBuildWall.New(0, targets)!;

            Assert.True(game.ExecuteAction(action));

            Assert.Equal(targets.Length, game.Buildings.Count);
            Assert.All(game.Buildings, building =>
            {
                Assert.Equal(wallSegments == 0 ? typeof(Building.Fort) : typeof(Building.Wall), building.GetType());
                Assert.Equal(2, building.Health);
                Assert.Equal(0, building.HighestDirectDamage);
                Assert.Equal(0, building.Owner);
                Assert.Contains(building.Pos, targets);
                Assert.Equal(building.Id, board.GetGrid(building.Pos)!.GridBuilding);
            });
            Assert.Equal(1, game.CurrentPlayerId);
        }
    }
}
