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
    public class InkFlowTests
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
                    InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            })));
            return builder.Build()!;
        }

        private static Ink GetInk(Game game) => Assert.IsType<Ink>(game.Roles[0]);

        [Fact]
        public static void Builder_CreatesInkAndIndependentRoleClone()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Ink clone = Assert.IsType<Ink>(role.Clone());

            Assert.Equal(0, role.Id);
            Assert.Equal(0, role.PlayerId);
            Assert.Equal(role.Id, role.Unit.RoleId);
            Assert.Equal(0, role.Unit.Id);
            Assert.Equal(InkConsts.InkHealth, role.Unit.Health);
            Assert.Equal(InkConsts.InkAttackRange, role.Unit.AttackRange);
            Assert.Equal(game.Board.At(3, 3), role.Unit.Position);
            Assert.Equal(1, role.GetUnits().Length);
            Assert.Same(role.Unit, role.GetUnits()[0]);
            Assert.Equal(new[] { role.Unit.Id }, game.Board.GetGrid(role.Unit.Position)!.Units);
            Assert.Equal(role.Id, clone.Id);
            Assert.Equal(role.PlayerId, clone.PlayerId);
            Assert.NotSame(role.Unit, clone.Unit);
            Assert.Equal(role.Unit.Position, clone.Unit.Position);
            Assert.Equal(role.Unit.Health, clone.Unit.Health);
            Assert.True(clone.IsFirstAction);
        }

        [Fact]
        public static void Builder_RejectsInvalidStructureAndIncompleteConfiguration()
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            Assert.Null(InkBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(InkBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(InkBuilder.New(new UnitBuilder[]
            {
                new InkUnitBuilder(), new InkUnitBuilder(),
            }, position));

            InkUnitBuilder unitBuilder = new();
            InkBuilder roleBuilder = InkBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
            Assert.Null(unitBuilder.Build());
            Assert.Null(roleBuilder.Build());
            Assert.NotNull(roleBuilder.SetId(0));
            Assert.Null(roleBuilder.Build());
            Assert.NotNull(roleBuilder.SetPlayerId(0));
            Assert.Null(roleBuilder.Build());
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.Null(unitBuilder.Build());
            Assert.NotNull(unitBuilder.SetRoleId(0));
            Ink role = Assert.IsType<Ink>(roleBuilder.Build());
            Assert.Equal(position, role.Unit.Position);
            Assert.Same(role, roleBuilder.Build());
            Assert.Same(role.Unit, unitBuilder.Build());
        }

        [Fact]
        public static void Builder_RejectsConflictingUnitPosition()
        {
            Board board = Board.New(3, 3);
            InkUnitBuilder unitBuilder = new();
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.NotNull(unitBuilder.SetRoleId(0));
            Assert.NotNull(unitBuilder.SetPosition(board.At(1, 1)!.Value));
            InkBuilder roleBuilder = InkBuilder.New(new UnitBuilder[] { unitBuilder }, board.At(2, 2)!.Value)!;
            Assert.NotNull(roleBuilder.SetId(0));
            Assert.NotNull(roleBuilder.SetPlayerId(0));

            Assert.Null(roleBuilder.Build());
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
            board.GetGrid(position)!.Terrain = terrain;
            InkBuilder builder = InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, position)!;

            Assert.Equal(allowed, builder.CanInitialStay(board));
            Assert.False(builder.CanInitialStay(Board.New(1, 1)));
        }

        [Fact]
        public static void GeneralActions_AreRegisteredForInkAndOtherRoleActionsAreRejected()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            GameAction[] actions =
            {
                GeneralMove.New(0, target),
                GeneralEnd.New(0),
                GeneralSurrender.New(0),
                GeneralBuildFort.New(0, target),
                GeneralBuildWall.New(0, new[] { target })!,
                GeneralDemolish.New(0, target),
            };

            Assert.All(actions, action => Assert.True(role.Unit.HasAction(action)));
            Assert.False(game.ExecuteAction(WolfBite.New(0, target)));
            Assert.False(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.Empty(game.Events);
            Assert.True(role.IsFirstAction);
        }

        [Fact]
        public static void Move_UpdatesBothGridsAndPreservesConsumedBudgetInClone()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Board.Node origin = role.Unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;

            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));

            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(origin)!.Units);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(target)!.Units);
            Assert.False(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.Empty(role.Unit.GetView(game).CanBuildFortGrids);
            Assert.Empty(role.Unit.GetView(game).CanBuildWallGrids);
            Assert.Empty(role.Unit.GetView(game).CanDemolishGrids);
            Assert.True(role.HasActionLeft(game));
            GeneralMoveEvent moved = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(0, moved.UnitId);
            Assert.Equal(new[] { origin, target }, moved.Path);
            Ink clone = (Ink)role.Clone();
            Assert.False(clone.JudgeAction(GeneralMove.New(0, origin), game));
            Assert.False(role.JudgeAction(GeneralMove.New(0, origin), game));

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.IsFirstAction);
            Assert.True(role.JudgeAction(GeneralMove.New(0, origin), game));
            Assert.False(clone.IsFirstAction);
            Assert.Empty(clone.Unit.GetView(game).CanMoveGrids);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("two-steps")]
        [InlineData("diagonal")]
        [InlineData("wall")]
        [InlineData("lake")]
        [InlineData("line-wall")]
        [InlineData("enemy-fort")]
        public static void Move_InvalidTarget_IsRejectedWithoutSideEffects(string obstruction)
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Board.Node origin = role.Unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "self": target = origin; break;
                case "two-steps": target = game.Board.At(3, 5)!.Value; break;
                case "diagonal": target = game.Board.At(2, 4)!.Value; break;
                case "wall": game.Board.GetGrid(target)!.Terrain = Terrain.Wall; break;
                case "lake": game.Board.GetGrid(target)!.Terrain = Terrain.Lake; break;
                case "line-wall":
                    int wall = game.Board.VWallAt(origin)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
                case "enemy-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2)!.SetPos(target)!);
                    break;
            }

            Assert.False(game.ExecuteAction(GeneralMove.New(0, target)));

            Assert.Equal(origin, role.Unit.Position);
            Assert.DoesNotContain(role.Unit.GetView(game).CanMoveGrids, entry => entry.node == target);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(origin)!.Units);
            Assert.Empty(game.Events);
            Assert.True(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Build_CreatesBuildingAndEndsInkActions(bool wall)
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Board.Node target = game.Board.At(2, 3)!.Value;
            GameAction action = wall
                ? GeneralBuildWall.New(0, new[] { target })!
                : GeneralBuildFort.New(0, target);

            Assert.True(game.ExecuteAction(action));

            Building building = Assert.Single(game.Buildings);
            Assert.Equal(wall ? typeof(Building.Wall) : typeof(Building.Fort), building.GetType());
            Assert.Equal(0, building.Owner);
            Assert.Equal(target, building.Pos);
            Assert.True(role.Unit.End);
            Assert.False(role.IsFirstAction);
            Assert.False(role.HasActionLeft(game));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Single(game.Events);
        }

        [Fact]
        public static void Demolish_RemainsAvailableWhenMovementAndBuildingAreBlocked()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Board.Node target = role.Unit.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(target)!);
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.GetGrid(game.Board.At(row, col)!.Value)!.Terrain = Terrain.Wall;
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
            Ink role = GetInk(game);
            Board.Node fort = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(fort)!);
            UnitView view = Assert.IsType<InkUnitView>(role.Unit.GetView(game));
            UnitView clone = Assert.IsType<InkUnitView>(view.Clone());
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
            Ink role = GetInk(game);
            GameAction action = surrender ? GeneralSurrender.New(0) : GeneralEnd.New(0);

            Assert.True(game.ExecuteAction(action));

            UnitView view = role.Unit.GetView(game);
            Assert.Empty(((InkUnitView)view).CanAttackGrids);
            Assert.Empty(((InkUnitView)view).CanDashGrids);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
            Assert.False(role.HasActionLeft(game));
            Assert.Equal(!surrender, role.Unit.Alive);
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 4)!.Value), game));
            Assert.False(role.JudgeAction(InkDash.New(0, game.Board.At(3, 5)!.Value), game));
            Assert.False(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
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
            }
        }
    }
}
