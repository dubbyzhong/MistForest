using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class ArmigerThrustTests
    {
        private static Game MakeGame(bool armoredEnemy = false, int startRow = 3, int startCol = 3)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            RoleBuilder enemy = armoredEnemy
                ? ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(7, 7)!.Value)!
                : InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(7, 7)!.Value)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(startRow, startCol)!.Value)!,
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(1, 1)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    enemy,
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 6)!.Value)!,
                })!,
            })));
            return builder.Build()!;
        }

        private static Armiger Role(Game game) => Assert.IsType<Armiger>(game.Roles[0]);
        private static ArmigerUnit Unit(Game game) => Role(game).Unit;
        private static ArmigerUnitView View(Game game) => Assert.IsType<ArmigerUnitView>(Unit(game).GetView(game));

        private static void PlaceUnit(Game game, Unit unit, Board.Node target)
        {
            game.Board.GetGrid(unit.Position)!.Units.Remove(unit.Id);
            unit.Position = target;
            game.Board.GetGrid(target)!.Units.Add(unit.Id);
        }

        private static void PrepareArmor(Game game, int armor)
        {
            for (int layer = 0; layer < (armor + 1) / 2; ++layer)
            {
                Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            }
            if (armor % 2 != 0)
            {
                PlaceUnit(game, game.GetUnit(2)!, game.Board.At(3, 4)!.Value);
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(InkAttack.New(2, Unit(game).Position)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            }
            Assert.Equal(armor, Unit(game).Armor);
            game.Events.Clear();
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 3)]
        public static void Thrust_RangeUsesActualArmorAndDoesNotDamageUnitsBeyondIt(int armor, int range)
        {
            Game game = MakeGame();
            PrepareArmor(game, armor);
            Board.Node first = game.Board.At(3, 4)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, game.Board.At(3, 3 + range)!.Value);
            PlaceUnit(game, game.GetUnit(3)!, game.Board.At(3, 4 + range)!.Value);
            Assert.Contains(first, View(game).CanThrustGrids);
            Assert.True(Unit(game).HasAction(ArmigerThrust.New(0, first)));

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(0, hit.AttackerId);
            Assert.Equal(first, hit.Target);
            Assert.Equal(new[] { 2 }, hit.UnitIds);
            Assert.Null(hit.BuildingId);
            Assert.Equal(Enumerable.Range(1, range).Select(step => game.Board.At(3, 3 + step)!.Value), hit.HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(3, game.GetUnit(3)!.Health);
            Assert.Equal(armor, Unit(game).Armor);
            Assert.Equal(game.Board.At(3, 3), Unit(game).Position);
            Assert.True(Unit(game).End);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanThrustGrids);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(1, 0)]
        [InlineData(0, -1)]
        [InlineData(0, 1)]
        public static void Thrust_SupportsAllFourCardinalDirections(int dx, int dy)
        {
            Game game = MakeGame();
            PrepareArmor(game, 2);
            Board.Node start = Unit(game).Position;
            Board.Node first = game.Board.Move(start, dx, dy)!.Value;
            Board.Node last = game.Board.Move(start, dx * 2, dy * 2)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, last);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { first, last }, hit.HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("diagonal")]
        [InlineData("two-steps")]
        [InlineData("out-of-board")]
        [InlineData("terrain-wall")]
        [InlineData("vertical-line-wall")]
        [InlineData("horizontal-line-wall")]
        public static void Thrust_InvalidDirectionIsRejectedWithoutConsumingAnAction(string obstruction)
        {
            Game game = MakeGame();
            Board.Node start = Unit(game).Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "self": target = start; break;
                case "diagonal": target = game.Board.At(4, 4)!.Value; break;
                case "two-steps": target = game.Board.At(3, 5)!.Value; break;
                case "out-of-board": target = Board.New(8, 8).At(8, 8)!.Value; break;
                case "terrain-wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "vertical-line-wall":
                    int vertical = game.Board.VWallAt(start)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(vertical / 64, 1ul << (vertical % 64), out _));
                    break;
                case "horizontal-line-wall":
                    target = game.Board.At(4, 3)!.Value;
                    int horizontal = game.Board.HWallAt(start)!.Value;
                    Assert.True(game.Board.HorizontalWalls.SetInner(horizontal / 64, 1ul << (horizontal % 64), out _));
                    break;
            }

            Assert.DoesNotContain(target, View(game).CanThrustGrids);
            Assert.False(game.ExecuteAction(ArmigerThrust.New(0, target)));

            Assert.Equal(start, Unit(game).Position);
            Assert.True(Unit(game).IsFirstAction);
            Assert.True(View(game).CanArmorUp);
            Assert.NotEmpty(View(game).CanMoveGrids);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(game.Events);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Thrust_StopsBeforeAnIntermediateTerrainOrLineWall(bool lineWall)
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            Board.Node second = game.Board.At(3, 5)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, first);
            PlaceUnit(game, game.GetUnit(3)!, game.Board.At(3, 6)!.Value);
            if (lineWall)
            {
                int wall = game.Board.VWallAt(first)!.Value;
                Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
            }
            else
            {
                game.Board.SetTerrain(second, Terrain.Wall);
            }

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.Equal(new[] { first }, Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events)).HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(3, game.GetUnit(3)!.Health);
        }

        [Theory]
        [InlineData(Terrain.Ground)]
        [InlineData(Terrain.Whirlpool)]
        [InlineData(Terrain.Lake)]
        public static void Thrust_ContinuesThroughNonBlockingTerrain(Terrain terrain)
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            game.Board.SetTerrain(first, terrain);
            PlaceUnit(game, game.GetUnit(2)!, game.Board.At(3, 6)!.Value);

            Assert.Contains(first, View(game).CanThrustGrids);
            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.Equal(3, Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events)).HitNodes.Length);
            Assert.Equal(1, game.GetUnit(2)!.Health);
        }

        [Fact]
        public static void Thrust_ClipsAtBoardEdgeAndViewExcludesOutsideDirections()
        {
            Game game = MakeGame(startRow: 1, startCol: 6);
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(1, 7)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, first);
            Assert.Equal(3, View(game).CanThrustGrids.Length);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.Equal(new[] { first }, Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events)).HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
        }

        [Fact]
        public static void Thrust_DamagesEnemiesAcrossMultipleGridsAndProtectsAlliedUnits()
        {
            Game game = MakeGame();
            PrepareArmor(game, 2);
            Board.Node first = game.Board.At(3, 4)!.Value;
            Board.Node second = game.Board.At(3, 5)!.Value;
            PlaceUnit(game, game.GetUnit(1)!, first);
            PlaceUnit(game, game.GetUnit(2)!, first);
            PlaceUnit(game, game.GetUnit(3)!, second);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { 2, 3 }, hit.UnitIds);
            Assert.Equal(new[] { first, second }, hit.HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(1, game.GetUnit(3)!.Health);
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(3, Unit(game).Health);
        }

        [Fact]
        public static void Thrust_MultipleUnitsCanDieWithoutInvalidatingGridIteration()
        {
            Game game = MakeGame();
            Board.Node first = game.Board.At(3, 4)!.Value;
            PlaceUnit(game, game.GetUnit(1)!, game.Board.At(3, 5)!.Value);
            PlaceUnit(game, game.GetUnit(2)!, first);
            PlaceUnit(game, game.GetUnit(3)!, first);
            Assert.True(game.ExecuteAction(WolfBite.New(1, first)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            game.Events.Clear();

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.False(game.GetUnit(2)!.Alive);
            Assert.False(game.GetUnit(3)!.Alive);
            Assert.Empty(game.Board.GetGrid(first)!.Units);
            Assert.Equal(new[] { 2, 3 }, Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events)).UnitIds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Thrust_EmptyOrAlliedGridStillConsumesTheAttackAndEndsTurn(bool allied)
        {
            Game game = MakeGame();
            Board.Node first = game.Board.At(3, 4)!.Value;
            if (allied) PlaceUnit(game, game.GetUnit(1)!, first);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Empty(hit.UnitIds);
            Assert.Equal(new[] { first }, hit.HitNodes);
            Assert.True(Unit(game).End);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.False(Role(game).JudgeAction(ArmigerThrust.New(0, first), game));
            Assert.False(Role(game).JudgeAction(GeneralMove.New(0, first), game));
            Assert.False(Role(game).JudgeAction(ArmigerArmorDown.New(0, 2), game));
            Assert.Empty(View(game).CanThrustGrids);
        }

        public static IEnumerable<object[]> Buildings()
        {
            foreach (bool wall in new[] { false, true })
            foreach (int owner in new[] { 0, 1 })
            foreach (int distance in new[] { 1, 2 })
            foreach (int health in new[] { 1, 5 })
                yield return new object[] { wall, owner, distance, health };
        }

        [Theory]
        [MemberData(nameof(Buildings))]
        public static void Thrust_BuildingShieldsUnitsAndStopsTheRayEvenWhenDestroyed(
            bool wall, int owner, int distance, int health)
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            Board.Node buildingNode = game.Board.At(3, 3 + distance)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, buildingNode);
            PlaceUnit(game, game.GetUnit(3)!, game.Board.At(3, 4 + distance)!.Value);
            BuildingBuilder builder = wall ? WallBuilder.New() : FortBuilder.New();
            game.AddBuilding(builder.SetOwner(owner)!.SetHealth(health)!.SetPos(buildingNode)!);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(distance, hit.HitNodes.Length);
            Assert.Equal(buildingNode, hit.HitNodes.Last());
            Assert.Equal(0, hit.BuildingId);
            Assert.Empty(hit.UnitIds);
            Assert.Equal(3, game.GetUnit(2)!.Health);
            Assert.Equal(3, game.GetUnit(3)!.Health);
            Assert.Equal(0, game.GetUnit(2)!.HighestDirectDamage);
            if (health == 1)
            {
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
            }
            else
            {
                Assert.Equal(3, game.Buildings[0].Health);
                Assert.Equal(2, game.Buildings[0].HighestDirectDamage);
            }
        }

        [Fact]
        public static void Thrust_RecordsBothEarlierVictimsAndTheBlockingBuilding()
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            Board.Node buildingNode = game.Board.At(3, 5)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, first);
            PlaceUnit(game, game.GetUnit(3)!, buildingNode);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(buildingNode)!);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { 2 }, hit.UnitIds);
            Assert.Equal(0, hit.BuildingId);
            Assert.Equal(new[] { first, buildingNode }, hit.HitNodes);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(3, game.GetUnit(3)!.Health);
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
        }

        [Fact]
        public static void Thrust_RubbleDoesNotShieldUnitsOrStopTheRay()
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(first)!);
            game.Buildings[0].Destroyed(game, 0);
            PlaceUnit(game, game.GetUnit(2)!, game.Board.At(3, 6)!.Value);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(3, hit.HitNodes.Length);
            Assert.Null(hit.BuildingId);
            Assert.Equal(new[] { 2 }, hit.UnitIds);
            Assert.Equal(1, game.GetUnit(2)!.Health);
        }

        [Fact]
        public static void Thrust_UsesSharedArmorAbsorptionAndStartsVictimsArmorLock()
        {
            Game game = MakeGame(armoredEnemy: true);
            Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));
            Assert.True(game.ExecuteAction(ArmigerArmorUp.New(2)));
            ArmigerUnit enemy = Assert.IsType<Armiger>(game.Roles[2]).Unit;
            Board.Node first = game.Board.At(3, 4)!.Value;
            PlaceUnit(game, enemy, first);
            game.Events.Clear();

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.Equal(0, enemy.Armor);
            Assert.Equal(3, enemy.Health);
            Assert.Equal(2, enemy.ArmorLockRounds);
            Assert.Equal(2, enemy.HighestDirectDamage);
            Assert.Equal(new[] { 2 }, Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events)).UnitIds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Thrust_OnlyAddsDamageBeyondThePreviousAttackStrength(bool previousWolf)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            RoleBuilder first = previousWolf
                ? WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 5)!.Value)!
                : InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 5)!.Value)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder> { first })!,
                New(new List<RoleBuilder>
                {
                    ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 4)!.Value)!,
                })!,
            })));
            Game game = builder.Build()!;
            Board.Node target = game.GetUnit(2)!.Position;
            MistForest.Core.GameRefactored.Action previous = previousWolf ? WolfBite.New(0, target) : InkAttack.New(0, target);
            Assert.True(game.ExecuteAction(previous));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            Assert.True(game.ExecuteAction(ArmigerThrust.New(1, target)));

            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(2, game.GetUnit(2)!.HighestDirectDamage);
            Assert.Equal(new[] { 2 }, Assert.IsType<ArmigerThrustEvent>(game.Events.Last()).UnitIds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Thrust_RemainsAvailableAfterFullMovementAndResetsNextRound(bool split)
        {
            Game game = MakeGame();
            if (split)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));
            }
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 1)!.Value)));
            Board.Node first = game.Board.At(3, 2)!.Value;
            PlaceUnit(game, game.GetUnit(2)!, first);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(Role(game).HasActionLeft(game));
            Assert.Empty(View(game).CanMoveGrids);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));

            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanThrustGrids);
            Assert.False(Role(game).JudgeAction(ArmigerThrust.New(0, first), game));
            Assert.False(game.ExecuteAction(ArmigerThrust.New(0, first)));
            Armiger clone = Assert.IsType<Armiger>(Role(game).Clone());
            Assert.Empty(((ArmigerUnitView)clone.Unit.GetView(game)).CanThrustGrids);
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.NotEmpty(View(game).CanMoveGrids);
            Assert.Contains(first, View(game).CanThrustGrids);
            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, first)));
            Assert.False(game.GetUnit(2)!.Alive);
            Assert.Empty(((ArmigerUnitView)clone.Unit.GetView(game)).CanThrustGrids);
        }

        [Theory]
        [InlineData(4, 2, 2)]
        [InlineData(4, 4, 1)]
        [InlineData(3, 1, 2)]
        [InlineData(3, 3, 1)]
        public static void Thrust_RecomputesRangeAfterUnloading(int armor, int amount, int range)
        {
            Game game = MakeGame();
            PrepareArmor(game, armor);
            Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, amount)));
            PlaceUnit(game, game.GetUnit(2)!, game.Board.At(3, 4)!.Value);
            PlaceUnit(game, game.GetUnit(3)!, game.Board.At(3, 6)!.Value);

            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, game.Board.At(3, 4)!.Value)));

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(game.Events.Last());
            Assert.Equal(range, hit.HitNodes.Length);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            Assert.Equal(3, game.GetUnit(3)!.Health);
        }

        [Fact]
        public static void ViewAndCloneQueries_DoNotOverwriteTheValidatedPath()
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            Board.Node first = game.Board.At(3, 4)!.Value;
            ArmigerThrust thrust = ArmigerThrust.New(0, first);
            Assert.True(Role(game).JudgeAction(thrust, game));
            ArmigerUnitView view = View(game);
            ArmigerUnitView clone = Assert.IsType<ArmigerUnitView>(view.Clone());
            Board.Node original = view.CanThrustGrids[0];
            clone.CanThrustGrids[0] = Unit(game).Position;
            Assert.Equal(original, view.CanThrustGrids[0]);
            _ = View(game);
            _ = Role(game).Clone().GetUnits()[0].GetView(game);

            Role(game).ExecuteAction(thrust, game);

            ArmigerThrustEvent hit = Assert.IsType<ArmigerThrustEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { first, game.Board.At(3, 5)!.Value, game.Board.At(3, 6)!.Value }, hit.HitNodes);
            Assert.Empty(View(game).CanThrustGrids);
            Assert.NotEmpty(view.CanThrustGrids);
        }

        [Fact]
        public static void ThrustEvent_CopiesVictimAndGridArrays()
        {
            Board board = Board.New(5, 5);
            Board.Node first = board.At(3, 4)!.Value;
            int[] victims = { 2, 3 };
            Board.Node[] path = { first, board.At(3, 5)!.Value };
            ArmigerThrustEvent hit = ArmigerThrustEvent.New(0, victims, 1, first, path);
            victims[0] = 99;
            path[0] = board.At(1, 1)!.Value;

            Assert.Equal(new[] { 2, 3 }, hit.UnitIds);
            Assert.Equal(first, hit.HitNodes[0]);
            Assert.Equal(first, hit.Target);
            Assert.Equal(1, hit.BuildingId);
        }
    }
}
