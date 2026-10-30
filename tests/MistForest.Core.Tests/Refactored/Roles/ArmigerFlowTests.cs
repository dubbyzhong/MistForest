using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class ArmigerFlowTests
    {
        private static Game MakeGame()
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
                    ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            })));
            return builder.Build()!;
        }

        private static Armiger Role(Game game) => Assert.IsType<Armiger>(game.Roles[0]);

        private static void AssertNoActions(UnitView view)
        {
            Assert.Empty(Assert.IsType<ArmigerUnitView>(view).CanThrustGrids);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
        }

        [Fact]
        public static void Builder_CreatesArmigerAndIndependentRoleAndUnitClones()
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Armiger clone = Assert.IsType<Armiger>(role.Clone());
            ArmigerUnit unitClone = Assert.IsType<ArmigerUnit>(role.Unit.Clone());

            Assert.Equal(0, role.Id);
            Assert.Equal(0, role.PlayerId);
            Assert.Equal(role.Id, role.Unit.RoleId);
            Assert.Equal(0, role.Unit.Id);
            Assert.Equal(ArmigerConsts.ArmigerHealth, role.Unit.Health);
            Assert.Equal(ArmigerConsts.ArmigerThrustBaseRange, role.Unit.AttackRange);
            Assert.Equal(game.Board.At(3, 3), role.Unit.Position);
            Assert.Same(role.Unit, Assert.Single(role.GetUnits().ToArray()));
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(role.Unit.Position)!.Units);
            Assert.Equal(role.Id, clone.Id);
            Assert.Equal(role.PlayerId, clone.PlayerId);
            Assert.Equal(role.Unit.Id, unitClone.Id);
            Assert.Equal(role.Unit.RoleId, unitClone.RoleId);
            Assert.Equal(role.Unit.Health, unitClone.Health);
            Assert.Equal(role.Unit.Position, clone.Unit.Position);
            Assert.NotSame(role.Unit, clone.Unit);
            Assert.NotSame(role.Unit, unitClone);
            Assert.True(clone.IsFirstAction);
            Assert.IsType<ArmigerUnitView>(role.Unit.GetView(game));
        }

        [Fact]
        public static void Builder_RejectsInvalidStructureAndIncompleteConfiguration()
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            Assert.Null(ArmigerBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(ArmigerBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(ArmigerBuilder.New(new UnitBuilder[]
            {
                new ArmigerUnitBuilder(), new ArmigerUnitBuilder(),
            }, position));

            ArmigerUnitBuilder unitBuilder = new();
            ArmigerBuilder builder = ArmigerBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
            Assert.Null(unitBuilder.Build());
            Assert.Null(builder.Build());
            Assert.NotNull(builder.SetId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(builder.SetPlayerId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.Null(unitBuilder.Build());
            Assert.NotNull(unitBuilder.SetRoleId(0));

            Armiger role = Assert.IsType<Armiger>(builder.Build());
            Assert.Equal(position, role.Unit.Position);
            Assert.Same(role, builder.Build());
            Assert.Same(role.Unit, unitBuilder.Build());
        }

        [Fact]
        public static void Builder_RejectsConflictingUnitPosition()
        {
            Board board = Board.New(3, 3);
            ArmigerUnitBuilder unitBuilder = new();
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.NotNull(unitBuilder.SetRoleId(0));
            Assert.NotNull(unitBuilder.SetPosition(board.At(1, 1)!.Value));
            ArmigerBuilder builder = ArmigerBuilder.New(new UnitBuilder[] { unitBuilder }, board.At(2, 2)!.Value)!;
            Assert.NotNull(builder.SetId(0));
            Assert.NotNull(builder.SetPlayerId(0));

            Assert.Null(builder.Build());
        }

        [Theory]
        [InlineData(Terrain.Ground, true)]
        [InlineData(Terrain.Whirlpool, true)]
        [InlineData(Terrain.Wall, false)]
        [InlineData(Terrain.Lake, false)]
        public static void Builder_ChecksInitialTerrain(Terrain terrain, bool allowed)
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            board.SetTerrain(position, terrain);
            ArmigerBuilder builder = ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, position)!;

            Assert.Equal(allowed, builder.CanInitialStay(board));
            Assert.False(builder.CanInitialStay(Board.New(1, 1)));
        }

        [Fact]
        public static void GeneralActions_AreRegisteredAndOtherRoleActionsAreRejected()
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            GameAction[] supported =
            {
                GeneralMove.New(0, target), GeneralEnd.New(0), GeneralSurrender.New(0),
                GeneralBuildFort.New(0, target), GeneralBuildWall.New(0, new[] { target })!,
                GeneralDemolish.New(0, target),
            };
            GameAction[] rejected =
            {
                WolfBite.New(0, target), AlphaAttack.New(0, target), AlphaBlood.New(0),
                InkAttack.New(0, target), InkDash.New(0, target),
            };

            Assert.All(supported, action => Assert.True(role.Unit.HasAction(action)));
            Assert.All(rejected, action =>
            {
                Assert.False(role.Unit.HasAction(action));
                Assert.False(role.JudgeAction(action, game));
                Assert.False(game.ExecuteAction(action));
            });
            Assert.Empty(game.Events);
            Assert.True(role.IsFirstAction);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Move_TwoStepBudgetSupportsOneOrTwoActionsAndResetsNextRound(bool split)
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Board.Node origin = role.Unit.Position;
            Board.Node middle = game.Board.At(3, 4)!.Value;
            Board.Node target = game.Board.At(3, 5)!.Value;
            Armiger? cloneAfterOneStep = null;
            if (split)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, middle)));
                Assert.Equal(0, game.CurrentPlayerId);
                Assert.True(role.HasActionLeft(game));
                Assert.False(role.IsFirstAction);
                Assert.All(role.Unit.GetView(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
                Assert.Empty(role.Unit.GetView(game).CanBuildFortGrids);
                Assert.Empty(role.Unit.GetView(game).CanBuildWallGrids);
                Assert.Empty(role.Unit.GetView(game).CanDemolishGrids);
                cloneAfterOneStep = (Armiger)role.Clone();
                Assert.False(cloneAfterOneStep.JudgeAction(
                    GeneralMove.New(0, game.Board.At(3, 6)!.Value), game));
            }

            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));

            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(origin)!.Units);
            Assert.Empty(game.Board.GetGrid(middle)!.Units);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(target)!.Units);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.HasActionLeft(game));
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.NotEmpty(((ArmigerUnitView)role.Unit.GetView(game)).CanThrustGrids);
            Assert.False(game.ExecuteAction(GeneralMove.New(0, middle)));
            GeneralMoveEvent[] moves = game.Events.OfType<GeneralMoveEvent>().ToArray();
            Assert.Equal(split ? 2 : 1, moves.Length);
            Assert.Equal(split ? new[] { middle, target } : new[] { origin, middle, target }, moves.Last().Path);

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            AssertNoActions(role.Unit.GetView(game));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.IsFirstAction);
            Assert.Contains(role.Unit.GetView(game).CanMoveGrids, entry => entry.distance == 2);
            if (cloneAfterOneStep is not null)
            {
                Assert.Equal(middle, cloneAfterOneStep.Unit.Position);
                Assert.False(cloneAfterOneStep.IsFirstAction);
                Assert.All(cloneAfterOneStep.Unit.GetView(game).CanMoveGrids,
                    entry => Assert.Equal(1, entry.distance));
            }
        }

        [Theory]
        [InlineData("self")]
        [InlineData("three-steps")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("middle-wall")]
        [InlineData("line-wall")]
        [InlineData("enemy-fort")]
        [InlineData("allied-wall")]
        [InlineData("out-of-board")]
        public static void Move_InvalidTargetIsRejectedWithoutSideEffects(string obstruction)
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Board.Node origin = role.Unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "self": target = origin; break;
                case "three-steps": target = game.Board.At(3, 6)!.Value; break;
                case "lake": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "terrain-wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "middle-wall":
                    game.Board.SetTerrain(target, Terrain.Wall);
                    target = game.Board.At(3, 5)!.Value;
                    break;
                case "line-wall":
                    int wall = game.Board.VWallAt(origin)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
                case "enemy-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(3)!.SetPos(target)!);
                    break;
                case "allied-wall":
                    game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(target)!);
                    break;
                case "out-of-board": target = Board.New(8, 8).At(8, 8)!.Value; break;
            }

            Assert.DoesNotContain(role.Unit.GetView(game).CanMoveGrids, entry => entry.node == target);
            Assert.False(game.ExecuteAction(GeneralMove.New(0, target)));
            Assert.Equal(origin, role.Unit.Position);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(origin)!.Units);
            Assert.True(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void Move_CanTurnAlongAnAlternativeTwoStepPathThroughAlliedFort()
        {
            Game game = MakeGame();
            Board.Node origin = Role(game).Unit.Position;
            Board.Node middle = game.Board.At(4, 3)!.Value;
            Board.Node target = game.Board.At(4, 4)!.Value;
            game.Board.SetTerrain(game.Board.At(3, 4)!.Value, Terrain.Wall);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(middle)!);

            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));

            GeneralMoveEvent moved = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { origin, middle, target }, moved.Path);
            Assert.Equal(target, Role(game).Unit.Position);
            Assert.Equal(3, game.Buildings[0].Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Build_CreatesBuildingAndEndsTurn(bool wall)
        {
            Game game = MakeGame();
            Board.Node target = game.Board.At(2, 3)!.Value;
            GameAction action = wall
                ? GeneralBuildWall.New(0, new[] { target })!
                : GeneralBuildFort.New(0, target);

            Assert.True(game.ExecuteAction(action));

            Building built = Assert.Single(game.Buildings);
            Assert.Equal(wall ? typeof(Building.Wall) : typeof(Building.Fort), built.GetType());
            Assert.Equal(0, built.Owner);
            Assert.Equal(target, built.Pos);
            Assert.True(Role(game).Unit.End);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Equal(1, game.CurrentPlayerId);
            AssertNoActions(Role(game).Unit.GetView(game));
            if (wall)
            {
                Assert.True(Assert.IsType<GeneralBuildWallEvent>(Assert.Single(game.Events)).Success);
            }
            else
            {
                Assert.True(Assert.IsType<GeneralBuildFortEvent>(Assert.Single(game.Events)).Success);
            }
        }

        [Fact]
        public static void Build_AfterMovingIsRejectedWhileAnotherMoveRemains()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 4)!.Value)));
            Board.Node target = game.Board.At(2, 4)!.Value;

            Assert.False(game.ExecuteAction(GeneralBuildFort.New(0, target)));
            Assert.False(game.ExecuteAction(GeneralBuildWall.New(0, new[] { target })!));
            Assert.Empty(game.Buildings);
            Assert.Single(game.Events);
            Assert.NotEmpty(Role(game).Unit.GetView(game).CanMoveGrids);
        }

        [Fact]
        public static void Demolish_RemainsAvailableWhenMovementAndBuildingAreBlocked()
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Board.Node target = role.Unit.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(target)!);
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.SetTerrain(game.Board.At(row, col)!.Value, Terrain.Wall);
            }
            UnitView view = role.Unit.GetView(game);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Equal(new[] { target }, view.CanDemolishGrids);
            Assert.True(role.HasActionLeft(game));

            Assert.True(game.ExecuteAction(GeneralDemolish.New(0, target)));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(target, role.Unit.Position);
            Assert.False(role.HasActionLeft(game));
            Assert.IsType<GeneralDemolishEvent>(Assert.Single(game.Events));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void ViewClone_OwnsAllGeneralActionArrays()
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            Board.Node fort = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(fort)!);
            UnitView view = Assert.IsType<ArmigerUnitView>(role.Unit.GetView(game));
            UnitView clone = Assert.IsType<ArmigerUnitView>(view.Clone());
            var originalMove = view.CanMoveGrids[0];
            Board.Node originalFort = view.CanBuildFortGrids[0];
            Board.Node originalWall = view.CanBuildWallGrids[0];

            clone.CanMoveGrids[0] = (role.Unit.Position, 0, null);
            clone.CanBuildFortGrids[0] = role.Unit.Position;
            clone.CanBuildWallGrids[0] = role.Unit.Position;
            clone.CanDemolishGrids[0] = role.Unit.Position;

            Assert.Equal(originalMove, view.CanMoveGrids[0]);
            Assert.Equal(originalFort, view.CanBuildFortGrids[0]);
            Assert.Equal(originalWall, view.CanBuildWallGrids[0]);
            Assert.Equal(new[] { fort }, view.CanDemolishGrids);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void EndAndSurrender_ClearAvailableActions(bool surrender)
        {
            Game game = MakeGame();
            Armiger role = Role(game);
            GameAction action = surrender ? GeneralSurrender.New(0) : GeneralEnd.New(0);

            Assert.True(game.ExecuteAction(action));

            AssertNoActions(role.Unit.GetView(game));
            Assert.False(role.HasActionLeft(game));
            Assert.Equal(!surrender, role.Unit.Alive);
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 4)!.Value), game));
            Assert.Single(game.Events);
            if (surrender)
            {
                Assert.False(game.Players[0].Alive);
                Assert.Empty(game.Board.GetGrid(role.Unit.Position)!.Units);
            }
            else
            {
                Assert.True(role.Unit.End);
                Assert.Equal(1, game.CurrentPlayerId);
                Armiger clone = (Armiger)role.Clone();
                Assert.True(clone.Unit.End);
                AssertNoActions(clone.Unit.GetView(game));
            }
        }
    }
}
