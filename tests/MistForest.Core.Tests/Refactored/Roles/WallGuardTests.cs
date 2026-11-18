using System.Reflection;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.Consts.CombatConsts;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class WallGuardTests
    {
        private static UnitBuilder[] NewSoldiers()
            => new UnitBuilder[] { new WallGuardUnitBuilder(), new WallGuardUnitBuilder(), new WallGuardUnitBuilder() };

        private static (Game game, Unit[] soldiers, Unit opponent) MakeGame(
            (int row, int col) opponentPosition, string opponent = "wolf",
            (int row, int col)[]? deployment = null)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            deployment ??= new[] { (2, 2), (4, 4), (6, 6) };
            Node[] positions = deployment.Select(point => board.At(point.row, point.col)!.Value).ToArray();
            Node enemyPosition = board.At(opponentPosition.row, opponentPosition.col)!.Value;
            RoleBuilder enemy = opponent switch
            {
                "sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, enemyPosition)!,
                "hidden" => HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, enemyPosition)!,
                _ => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, enemyPosition)!,
            };
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder> { WallGuardBuilder.New(NewSoldiers(), positions)! })!,
                New(new List<RoleBuilder> { enemy })!,
            })));
            Game game = builder.Build()!;
            WallGuard guard = Assert.IsType<WallGuard>(game.Roles[0]);
            return (game, guard.GetUnits().ToArray(), game.Roles[1].GetUnits()[0]);
        }

        private static Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;

        private static WallGuardUnitView View(Unit soldier, Game game)
            => Assert.IsType<WallGuardUnitView>(soldier.GetView(game));

        [Fact]
        public static void Soldiers_CanBuildAttackAndMoveInAnyOrderWithIndependentActions()
        {
            var (game, soldiers, enemy) = MakeGame((4, 5));
            Assert.Equal(new[] { 0, 1, 2 }, soldiers.Select(unit => unit.Id));
            Assert.All(soldiers, unit => Assert.Equal(96, unit.Health));
            Node greatWall = At(game, 2, 3);
            Node destination = At(game, 6, 5);

            Assert.True(game.ExecuteAction(GeneralMove.New(soldiers[2].Id, destination)));
            Assert.Equal(destination, soldiers[2].Position);
            Assert.False(game.ExecuteAction(GeneralMove.New(soldiers[2].Id, At(game, 6, 4))));
            Assert.Equal(0, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(WallGuardBuildGreatWall.New(soldiers[0].Id, greatWall)));
            Building.GreatWall built = Assert.IsType<Building.GreatWall>(Assert.Single(game.Buildings));
            Assert.Equal(built.Id, game.Board.GetGrid(greatWall)!.GridBuilding);
            Assert.Equal(UnitsPerPoint, built.Health);
            Assert.Equal(0, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(WallGuardAttack.New(soldiers[1].Id, enemy.Position)));
            Assert.Equal(HalfPoint, enemy.Health);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.All(soldiers, unit => Assert.True(unit.End));
            Assert.True(game.ExecuteAction(GeneralEnd.New(enemy.Id)));
            Assert.All(soldiers, unit => Assert.True(unit.IsFirstAction));
        }

        [Fact]
        public static void Demolition_SpendsOnlyTheCallerAndExplicitEndFinishesTheRole()
        {
            var (game, soldiers, _) = MakeGame((7, 7));
            Unit soldier = soldiers[1];
            Node fort = At(game, 4, 5);
            Node empty = At(game, 3, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(fort)!);
            Assert.False(game.ExecuteAction(GeneralBuildFort.New(soldier.Id, empty)));
            Assert.False(game.ExecuteAction(GeneralBuildWall.New(soldier.Id, new[] { empty })!));

            Assert.True(game.ExecuteAction(GeneralDemolish.New(soldier.Id, fort)));
            Assert.IsType<Building.Rubble>(Assert.Single(game.Buildings));
            Assert.True(soldier.End);
            Assert.False(soldiers[0].End);
            Assert.False(soldiers[2].End);
            Assert.Equal(0, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(GeneralEnd.New(soldier.Id)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(soldier.Id, Assert.IsType<GeneralEndEvent>(game.Events.Last()).UnitId);
        }

        [Fact]
        public static void HiddenOccupant_BlocksConstructionAndSpendsOnlyTheBuildingSoldier()
        {
            var (game, soldiers, hidden) = MakeGame((2, 3), "hidden");
            Unit soldier = soldiers[0];
            Assert.Contains(hidden.Position, View(soldier, game).CanBuildGreatWallGrids);
            Assert.True(game.ExecuteAction(WallGuardBuildGreatWall.New(soldier.Id, hidden.Position)));

            Assert.False(Assert.IsType<WallGuardBuildGreatWallEvent>(Assert.Single(game.Events)).Success);
            Assert.Null(game.Board.GetGrid(hidden.Position)!.GridBuilding);
            Assert.True(soldier.End);
            Assert.False(soldiers[1].End);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Surrender_RecordsTheActingSoldierAndEliminatesTheWholeRole()
        {
            var (game, soldiers, _) = MakeGame((7, 7));

            Assert.True(game.ExecuteAction(GeneralSurrender.New(soldiers[2].Id)));
            Assert.All(soldiers, soldier => Assert.False(soldier.Alive));
            Assert.False(game.Roles[0].Alive);
            Assert.Equal(soldiers[2].Id, Assert.IsType<GeneralSurrenderEvent>(Assert.Single(game.Events)).UnitId);
        }

        [Fact]
        public static void AlliedGreatWall_ExtendsRangeAndMakesAttackRanged()
        {
            var (game, soldiers, hidden) = MakeGame((2, 4), "hidden");
            Unit soldier = soldiers[0];
            WallGuardAttack attack = WallGuardAttack.New(soldier.Id, hidden.Position);
            Assert.Equal(1, View(soldier, game).AttackRange);
            Assert.False(game.ExecuteAction(attack));
            game.AddBuilding(GreatWallBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(soldier.Position)!);

            WallGuardUnitView view = View(soldier, game);
            Assert.Equal(2, view.AttackRange);
            Assert.Contains(hidden.Position, view.CanAttackGrids);
            Assert.Empty(view.CanBuildGreatWallGrids);
            Assert.True(game.ExecuteAction(attack));
            Assert.Equal(UnitsPerPoint, hidden.Health);
        }

        [Fact]
        public static void Formation_SharesAlongDiagonalChainAndKeepsIndependentDirectDamageRecords()
        {
            var (game, soldiers, enemy) = MakeGame((3, 4), deployment: new[] { (3, 3), (4, 4), (5, 5) });
            Assert.True(game.ExecuteAction(GeneralEnd.New(soldiers[0].Id)));

            Assert.True(game.ExecuteAction(WolfBite.New(enemy.Id, soldiers[0].Position)));
            Assert.All(soldiers, unit => Assert.Equal(56, unit.Health));
            Assert.Equal(UnitsPerPoint, soldiers[0].HighestDirectDamage);
            Assert.Equal(0, soldiers[1].HighestDirectDamage);
            Assert.Equal(0, soldiers[2].HighestDirectDamage);
            Assert.True(game.ExecuteAction(WolfBite.New(enemy.Id, soldiers[0].Position)));
            Assert.All(soldiers, unit => Assert.Equal(56, unit.Health));
            Assert.True(game.ExecuteAction(WolfBite.New(enemy.Id, soldiers[1].Position)));
            Assert.All(soldiers, unit => Assert.Equal(16, unit.Health));

            Assert.True(game.ExecuteAction(GeneralEnd.New(soldiers[0].Id)));
            Assert.True(game.ExecuteAction(WolfBite.New(enemy.Id, soldiers[0].Position)));
            Assert.False(soldiers[0].Alive);
            Assert.Equal(16, soldiers[1].Health);
            Assert.Equal(16, soldiers[2].Health);
            Assert.True(game.Roles[0].Alive);
            Assert.DoesNotContain(soldiers[0].Id, game.Board.GetGrid(soldiers[0].Position)!.Units);
        }

        [Theory]
        [InlineData(false, 15)]
        [InlineData(true, 10)]
        public static void Formation_SplitsQuarterPointExactlyAmongConnectedSoldiers(bool allConnected, int share)
        {
            var (game, soldiers, _) = MakeGame((7, 7), deployment: new[]
            {
                (3, 3), (4, 4), allConnected ? (5, 5) : (6, 6),
            });
            MethodInfo hit = typeof(Unit).GetMethod("ReceiveDirectDamage",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            MethodInfo silence = typeof(Unit).GetMethod("TrySilence",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)silence.Invoke(soldiers[1], null)!);
            DamageResult result = (DamageResult)hit.Invoke(soldiers[0],
                new object[] { QuarterPoint, game, DamageType.Melee })!;

            Assert.Equal(QuarterPoint, result.HealthDamage);
            Assert.Equal(96 - share, soldiers[0].Health);
            Assert.Equal(96 - share, soldiers[1].Health);
            Assert.Equal(allConnected ? 96 - share : 96, soldiers[2].Health);
            Assert.Equal(QuarterPoint, soldiers[0].HighestDirectDamage);
            Assert.Equal(0, soldiers[1].HighestDirectDamage);
        }

        [Fact]
        public static void Execution_KillsOneSoldierWithoutSharingAndLeavesRoleAlive()
        {
            var (game, soldiers, sniper) = MakeGame((3, 4), "sniper", new[] { (3, 3), (4, 4), (5, 5) });
            Assert.True(game.ExecuteAction(GeneralEnd.New(soldiers[0].Id)));
            Assert.True(game.ExecuteAction(SniperExecute.New(sniper.Id, soldiers[0].Position)));
            Assert.False(soldiers[0].Alive);
            Assert.Equal(96, soldiers[1].Health);
            Assert.Equal(96, soldiers[2].Health);
            Assert.True(game.Roles[0].Alive);
            Assert.True(game.ExecuteAction(GeneralMove.New(soldiers[2].Id, At(game, 5, 6))));
        }

        [Fact]
        public static void Builder_RejectsInvalidDeploymentAndCloneKeepsIndependentSoldiers()
        {
            var (game, soldiers, _) = MakeGame((7, 7));
            Node[] positions = soldiers.Select(unit => unit.Position).ToArray();
            Assert.Null(WallGuardBuilder.New(NewSoldiers(), positions.Take(2).ToArray()));
            UnitBuilder duplicate = new WallGuardUnitBuilder();
            Assert.Null(WallGuardBuilder.New(new[] { duplicate, duplicate, duplicate }, positions));
            WallGuardBuilder builder = WallGuardBuilder.New(NewSoldiers(), positions)!;
            game.Board.SetTerrain(positions[2], Terrain.Wall);
            Assert.False(builder.CanInitialStay(game.Board));
            game.Board.SetTerrain(positions[2], Terrain.Ground);

            WallGuard clone = Assert.IsType<WallGuard>(game.Roles[0].Clone());
            Assert.True(game.ExecuteAction(GeneralMove.New(soldiers[2].Id, At(game, 6, 5))));
            Assert.Equal(positions[2], clone.GetUnits()[2].Position);
            Assert.False(clone.GetUnits()[2].End);
            WallGuardUnitView view = View(soldiers[0], game);
            WallGuardUnitView clonedView = Assert.IsType<WallGuardUnitView>(view.Clone());
            Assert.NotSame(view.CanAttackGrids, clonedView.CanAttackGrids);
            Assert.NotSame(view.CanBuildGreatWallGrids, clonedView.CanBuildGreatWallGrids);
        }
    }
}
