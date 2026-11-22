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
    public class FireflyFlowTests
    {
        private static Game MakeGame(int firstPlayerId, params Func<Board, RoleBuilder>[] opponents)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder> { FireflyAt(board, 3, 3) })!,
            };
            foreach (Func<Board, RoleBuilder> opponent in opponents)
            {
                players.Add(New(new List<RoleBuilder> { opponent(board) })!);
            }

            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return Assert.IsType<Game>(builder.Build());
        }

        private static FireflyBuilder FireflyAt(Board board, int row, int col)
            => FireflyBuilder.New(new UnitBuilder[] { new FireflyUnitBuilder() }, board.At(row, col)!.Value)!;

        private static WolfBuilder WolfAt(Board board, int row, int col)
            => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(row, col)!.Value)!;

        private static Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static Firefly Role(Game game) => Assert.IsType<Firefly>(game.Roles[0]);
        private static FireflyUnitView View(Game game)
            => Assert.IsType<FireflyUnitView>(Role(game).Unit.GetView(game));

        [Fact]
        public static void Spray_RequiresPrivateAdvanceThenAllowsOneFireStepAndBurningTileRecovery()
        {
            Game game = MakeGame(0, board => WolfAt(board, 7, 7));
            Node origin = At(game, 3, 3);
            Node first = At(game, 3, 4);
            Node second = At(game, 3, 5);
            Node third = At(game, 3, 6);
            FireflyUnitView view = View(game);
            FireflyUnitView clone = Assert.IsType<FireflyUnitView>(view.Clone());
            Node[] path = Assert.Single(view.CanSprayDirections, entry => entry.first == first).path;
            Assert.Equal(new[] { first, second, third }, path);
            Assert.NotSame(path, Assert.Single(clone.CanSprayDirections, entry => entry.first == first).path);
            Assert.False(game.ExecuteAction(FireflySpray.New(0, first, 0)));
            Assert.False(game.ExecuteAction(FireflySpray.New(0, first, 4)));
            Assert.False(game.ExecuteAction(FireflySpray.New(0, At(game, 4, 4), 2)));
            Assert.True(game.ExecuteAction(FireflySpray.New(0, first, 2)));

            Assert.Equal(1, View(game).Ammo);
            Assert.Equal(new[] { origin, first, second }, View(game).CanAdvanceGrids);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanStepGrids);
            Assert.False(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.False(game.ExecuteAction(FireflyAdvance.New(0, third)));
            Firefly roleClone = Assert.IsType<Firefly>(Role(game).Clone());
            Assert.Equal(View(game).CanAdvanceGrids,
                Assert.IsType<FireflyUnitView>(roleClone.Unit.GetView(game)).CanAdvanceGrids);

            Assert.True(game.ExecuteAction(FireflyAdvance.New(0, second)));
            Assert.False(game.ExecuteAction(FireflyAdvance.New(0, first)));
            Assert.False(game.ExecuteAction(GeneralMove.New(0, first)));
            Assert.True(game.ExecuteAction(FireflyStep.New(0, first, ignite: false)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(3, View(game).Ammo);
            Assert.Equal(first, Role(game).Unit.Position);
            Assert.Equal(new[] { first, second }, Assert.Single(game.Events.OfType<FireflySprayEvent>()).Path);
            Assert.Equal(2, game.GetEventsForPlayer(0).OfType<FireflyMoveEvent>().Count());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<FireflyMoveEvent>());
            Assert.Equal(2, game.GetBurningGrids().Length);

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Empty(game.GetBurningGrids());
            Assert.Equal(new[] { first, second }, Assert.Single(game.Events.OfType<FireflyFireExpiredEvent>()).Targets);
        }

        [Fact]
        public static void Spray_DoesNotIgniteOrDamageTheCastersTile()
        {
            Game game = MakeGame(0, board => WolfAt(board, 3, 3), board => WolfAt(board, 3, 4));
            Node origin = At(game, 3, 3);
            Node first = At(game, 3, 4);

            Assert.False(game.ExecuteAction(FireflySpray.New(0, origin, 1)));
            Assert.True(game.ExecuteAction(FireflySpray.New(0, first, 1)));

            Assert.Equal((first, 0), Assert.Single(game.GetBurningGrids()));
            Assert.Equal(first, Assert.Single(Assert.Single(game.Events.OfType<FireflySprayEvent>()).Path));
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(HalfPoint, game.GetUnit(2)!.Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Spray_StopsBeforeTerrainAndLineWalls(bool lineWall)
        {
            Game game = MakeGame(0, board => WolfAt(board, 7, 7));
            Node first = At(game, 3, 4);
            if (lineWall)
            {
                int edge = game.Board.VWallAt(first)!.Value;
                Assert.True(game.Board.VerticalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _));
            }
            else
            {
                Assert.True(game.Board.SetTerrain(At(game, 3, 5), Terrain.Wall));
            }

            Assert.Equal(first, Assert.Single(Assert.Single(View(game).CanSprayDirections, entry => entry.first == first).path));
            Assert.True(game.ExecuteAction(FireflySpray.New(0, first, 3)));
            Assert.Equal((first, 0), Assert.Single(game.GetBurningGrids()));
            Assert.Equal(0, View(game).Ammo);
        }

        [Fact]
        public static void Spray_CanCrossWaterAndStopsAtTheFirstBuildingEvenWhenItFalls()
        {
            Game game = MakeGame(0, board => WolfAt(board, 3, 5), board => WolfAt(board, 3, 6));
            Node lake = At(game, 3, 4);
            Node fort = At(game, 3, 5);
            Assert.True(game.Board.SetTerrain(lake, Terrain.Lake));
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(fort)!);
            Assert.True(game.ExecuteAction(FireflySpray.New(0, lake, 3)));

            Assert.Equal(new[] { lake, fort }, Assert.Single(game.Events.OfType<FireflySprayEvent>()).Path);
            Assert.Equal((fort, 0), Assert.Single(game.GetBurningGrids()));
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(3 * HalfPoint, game.GetUnit(2)!.Health);
            Assert.True(game.ExecuteAction(FireflyAdvance.New(0, fort)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(GeneralMove.New(1, At(game, 4, 5))));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
        }

        [Fact]
        public static void Step_IgnitionAfterOrdinaryMoveBlocksRecoveryAndBurnsOnVictimTurn()
        {
            Game game = MakeGame(0, board => WolfAt(board, 3, 5), board => WolfAt(board, 7, 7));
            Node first = At(game, 3, 4);
            Node target = At(game, 3, 5);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, first)));
            Assert.Empty(View(game).CanSprayDirections);
            Assert.False(game.ExecuteAction(FireflySpray.New(0, target, 1)));
            Assert.Contains(target, View(game).CanStepGrids);
            Assert.True(game.ExecuteAction(FireflyStep.New(0, target, ignite: true)));

            Assert.Equal(3, View(game).Ammo);
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(UnitsPerPoint, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(game.Events.OfType<FireflyDamageEvent>().Where(entry => entry.IsBurningDamage));

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.False(game.GetUnit(1)!.Alive);
            FireflyDamageEvent burn = Assert.Single(game.Events.OfType<FireflyDamageEvent>(), entry => entry.IsBurningDamage);
            Assert.Equal(1, Assert.Single(burn.UnitIds));
            Assert.Equal((target, 0), Assert.Single(game.GetBurningGrids()));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Empty(game.GetBurningGrids());
        }

        [Fact]
        public static void Step_CanPrecedeSprayButCannotBeRepeated()
        {
            Game game = MakeGame(0, board => WolfAt(board, 7, 7));
            Node step = At(game, 3, 4);
            Node target = At(game, 3, 5);
            Assert.True(game.ExecuteAction(FireflyStep.New(0, step, ignite: false)));
            Assert.Equal(3, View(game).Ammo);
            Assert.Empty(game.GetBurningGrids());
            Assert.Empty(View(game).CanStepGrids);
            Assert.False(game.ExecuteAction(FireflyStep.New(0, target, ignite: false)));
            Assert.True(game.ExecuteAction(FireflySpray.New(0, target, 1)));
            Assert.True(game.ExecuteAction(FireflyAdvance.New(0, step)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(3, View(game).Ammo);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        public static void Step_CanBeUsedWithoutAmmo(bool ignite, int expectedAmmoAfterTurn)
        {
            Game game = MakeGame(0, board => WolfAt(board, 7, 7));
            Node origin = At(game, 3, 3);
            Node target = At(game, 4, 3);
            Assert.True(game.ExecuteAction(FireflySpray.New(0, At(game, 3, 4), 3)));
            Assert.True(game.ExecuteAction(FireflyAdvance.New(0, origin)));

            Assert.Equal(0, View(game).Ammo);
            Assert.Contains(target, View(game).CanStepGrids);
            Assert.True(game.ExecuteAction(FireflyStep.New(0, target, ignite)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(target, Role(game).Unit.Position);
            Assert.Equal(expectedAmmoAfterTurn, View(game).Ammo);
            Assert.Equal(ignite, game.GetBurningGrids().Any(fire => fire.node == target));
        }

        [Fact]
        public static void Firefly_ReceivesEnemyIgnitionButIsImmuneToBurning()
        {
            Game game = MakeGame(0, board => FireflyAt(board, 3, 4));
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(FireflyStep.New(0, target, ignite: true)));
            Assert.Equal(HalfPoint, game.GetUnit(1)!.Health);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(HalfPoint, game.GetUnit(1)!.Health);
            Assert.Empty(game.Events.OfType<FireflyDamageEvent>().Where(entry => entry.IsBurningDamage));
        }

        [Fact]
        public static void Burning_BuildingAndRoseEachTakeDamageAtTheirOwnersTurnEnd()
        {
            Game game = MakeGame(1, board => PrinceBuilder.New(
                new UnitBuilder[] { new PrinceUnitBuilder() }, board.At(4, 4)!.Value)!);
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(1, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(3 * HalfPoint)!.SetPos(target)!);

            Assert.True(game.ExecuteAction(FireflySpray.New(0, target, 1)));
            Assert.Equal(HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.Equal(HalfPoint, game.Buildings[0].Health);
            Assert.True(game.ExecuteAction(FireflyAdvance.New(0, At(game, 3, 3))));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.Equal(HalfPoint, game.Buildings[0].Health);

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Empty(game.GetRoses());
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Empty(game.GetBurningGrids());
        }

        [Fact]
        public static void Silence_BlocksFireSkillsAndKeepsTheOrdinaryMove()
        {
            Game game = MakeGame(0, board => WolfAt(board, 7, 7));
            Node target = At(game, 3, 4);
            var silence = typeof(Unit).GetMethod("TrySilence",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.True((bool)silence.Invoke(Role(game).Unit, null)!);
            Assert.Empty(View(game).CanSprayDirections);
            Assert.Empty(View(game).CanStepGrids);
            Assert.False(game.ExecuteAction(FireflySpray.New(0, target, 1)));
            Assert.False(game.ExecuteAction(FireflyStep.New(0, target, ignite: true)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));
            Assert.Equal(1, game.CurrentPlayerId);
        }
    }
}
