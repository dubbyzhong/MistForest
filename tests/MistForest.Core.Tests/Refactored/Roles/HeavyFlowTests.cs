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
    // Direct role checks complement the game-entry coverage in HeavyChargeTests.
    public class HeavyFlowTests
    {
        private static Game MakeGame(bool adjacentEnemy = false, int firstPlayerId = 0)
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
                    HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(adjacentEnemy ? 3 : 7, adjacentEnemy ? 4 : 7)!.Value)!,
                })!,
            })));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static Heavy Role(Game game) => Assert.IsType<Heavy>(game.Roles[0]);

        private static void ExecuteRoleAction(Game game, GameAction action)
        {
            Heavy role = Role(game);
            Assert.True(role.JudgeAction(action, game));
            role.ExecuteAction(action, game);
        }

        private static void AssertNoActions(UnitView view)
        {
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
        }

        [Fact]
        public static void Builder_CreatesHeavyWithInitialArmorAndIndependentClones()
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            Heavy clone = Assert.IsType<Heavy>(role.Clone());
            HeavyUnit unitClone = Assert.IsType<HeavyUnit>(role.Unit.Clone());

            Assert.Equal(0, role.Id);
            Assert.Equal(0, role.PlayerId);
            Assert.Equal(role.Id, role.Unit.RoleId);
            Assert.Equal(0, role.Unit.Id);
            Assert.Equal(4, role.Unit.Health);
            Assert.Equal(2, role.Unit.Armor);
            Assert.Equal(0, role.Unit.AttackRange);
            Assert.Equal(game.Board.At(3, 3)!.Value, role.Unit.Position);
            Assert.Same(role.Unit, Assert.Single(role.GetUnits().ToArray()));
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(role.Unit.Position)!.Units);
            Assert.NotSame(role.Unit, clone.Unit);
            Assert.NotSame(role.Unit, unitClone);
            Assert.Equal(role.Unit.Armor, clone.Unit.Armor);
            Assert.Equal(role.Unit.Health, unitClone.Health);
            Assert.Equal(role.PlayerId, clone.PlayerId);
            Assert.True(clone.IsFirstAction);
            Assert.Equal(2, Assert.IsType<HeavyUnitView>(role.Unit.GetView(game)).Armor);
        }

        [Fact]
        public static void Builder_RejectsWrongStructureAndConflictingPosition()
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            Assert.Null(HeavyBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(HeavyBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(HeavyBuilder.New(new UnitBuilder[]
            {
                new HeavyUnitBuilder(), new HeavyUnitBuilder(),
            }, position));
            HeavyUnitBuilder unitBuilder = new();
            HeavyBuilder builder = HeavyBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
            Assert.Null(builder.Build());
            Assert.Null(unitBuilder.Build());
            Assert.NotNull(builder.SetId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(builder.SetPlayerId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.NotNull(unitBuilder.SetRoleId(0));
            Heavy role = Assert.IsType<Heavy>(builder.Build());
            Assert.Same(role, builder.Build());
            Assert.Same(role.Unit, unitBuilder.Build());

            unitBuilder = new();
            unitBuilder.SetId(0)!.SetRoleId(0)!.SetPosition(board.At(1, 1)!.Value);
            builder = HeavyBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
            builder.SetId(0)!.SetPlayerId(0);
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
            HeavyBuilder builder = HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, position)!;
            Assert.Equal(allowed, builder.CanInitialStay(board));
            Assert.False(builder.CanInitialStay(Board.New(1, 1)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Move_ConsumesTwoStepBudgetAndRestoresItNextRound(bool split)
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            Board.Node origin = role.Unit.Position;
            Board.Node middle = game.Board.At(3, 4)!.Value;
            Board.Node target = game.Board.At(3, 5)!.Value;
            Heavy? cloneAfterOneStep = null;
            if (split)
            {
                ExecuteRoleAction(game, GeneralMove.New(0, middle));
                Assert.True(role.HasActionLeft(game));
                Assert.False(role.IsFirstAction);
                Assert.All(role.Unit.GetView(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
                cloneAfterOneStep = Assert.IsType<Heavy>(role.Clone());
            }
            ExecuteRoleAction(game, GeneralMove.New(0, target));

            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(origin)!.Units);
            Assert.Empty(game.Board.GetGrid(middle)!.Units);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(target)!.Units);
            Assert.False(role.JudgeAction(GeneralMove.New(0, middle), game));
            Assert.False(role.HasActionLeft(game));
            AssertNoActions(role.Unit.GetView(game));
            GeneralMoveEvent[] moves = game.Events.OfType<GeneralMoveEvent>().ToArray();
            Assert.Equal(split ? 2 : 1, moves.Length);
            Assert.Equal(split ? new[] { middle, target } : new[] { origin, middle, target }, moves.Last().Path);

            role.Unit.EndRound(game);
            Assert.True(role.IsFirstAction);
            Assert.Equal(HeavyConsts.HeavyInitialArmor, role.Unit.Armor);
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
        public static void Move_RejectsInvalidPathsWithoutSideEffects(string obstruction)
        {
            Game game = MakeGame();
            Heavy role = Role(game);
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
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2)!.SetPos(target)!);
                    break;
                case "allied-wall":
                    game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(target)!);
                    break;
                case "out-of-board": target = Board.New(8, 8).At(8, 8)!.Value; break;
            }
            Assert.DoesNotContain(role.Unit.GetView(game).CanMoveGrids, entry => entry.node == target);
            Assert.False(role.JudgeAction(GeneralMove.New(0, target), game));
            Assert.Equal(origin, role.Unit.Position);
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void Move_CanDetourThroughAlliedFortWithoutDamagingIt()
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            Board.Node origin = role.Unit.Position;
            Board.Node middle = game.Board.At(4, 3)!.Value;
            Board.Node target = game.Board.At(4, 4)!.Value;
            game.Board.SetTerrain(game.Board.At(3, 4)!.Value, Terrain.Wall);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(middle)!);
            ExecuteRoleAction(game, GeneralMove.New(0, target));
            Assert.Equal(new[] { origin, middle, target },
                Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events)).Path);
            Assert.Equal(2, game.Buildings[0].Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Build_ConsumesTurnAndIsRejectedAfterMovement(bool wall)
        {
            Game game = MakeGame();
            Board.Node target = game.Board.At(2, 3)!.Value;
            GameAction build = wall ? GeneralBuildWall.New(0, new[] { target })! : GeneralBuildFort.New(0, target);
            ExecuteRoleAction(game, build);
            Building built = Assert.Single(game.Buildings);
            Assert.Equal(wall ? typeof(Building.Wall) : typeof(Building.Fort), built.GetType());
            Assert.Equal(target, built.Pos);
            Assert.True(Role(game).Unit.End);
            Assert.False(Role(game).HasActionLeft(game));
            AssertNoActions(Role(game).Unit.GetView(game));

            game = MakeGame();
            ExecuteRoleAction(game, GeneralMove.New(0, game.Board.At(3, 4)!.Value));
            target = game.Board.At(2, 4)!.Value;
            build = wall ? GeneralBuildWall.New(0, new[] { target })! : GeneralBuildFort.New(0, target);
            Assert.False(Role(game).JudgeAction(build, game));
            Assert.Empty(game.Buildings);
            Assert.NotEmpty(Role(game).Unit.GetView(game).CanMoveGrids);
        }

        [Fact]
        public static void Demolish_OnlyAllowsAlliedBuildingsAndEndsActions()
        {
            Game game = MakeGame();
            Board.Node allied = game.Board.At(2, 3)!.Value;
            Board.Node enemy = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(allied)!);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2)!.SetPos(enemy)!);
            Assert.False(Role(game).JudgeAction(GeneralDemolish.New(0, enemy), game));
            ExecuteRoleAction(game, GeneralDemolish.New(0, allied));
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.IsType<Building.Fort>(game.Buildings[1]);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.IsType<GeneralDemolishEvent>(Assert.Single(game.Events));
        }

        [Fact]
        public static void InitialArmor_AbsorbsDamageAndDoesNotRegenerateOnCloneOrRoundReset()
        {
            Game game = MakeGame(adjacentEnemy: true, firstPlayerId: 1);
            Heavy role = Role(game);
            Board.Node target = role.Unit.Position;
            Assert.True(game.ExecuteAction(WolfBite.New(1, target)));
            Assert.Equal(0, role.Unit.Armor);
            Assert.Equal(4, role.Unit.Health);
            Heavy clone = Assert.IsType<Heavy>(role.Clone());
            HeavyUnitView snapshot = Assert.IsType<HeavyUnitView>(role.Unit.GetView(game));
            Assert.Equal(0, clone.Unit.Armor);
            Assert.Equal(2, clone.Unit.HighestDirectDamage);
            Assert.Equal(0, snapshot.Armor);

            Assert.True(game.ExecuteAction(WolfBite.New(1, target)));
            Assert.Equal(4, role.Unit.Health);
            role.Unit.EndRound(game);
            Assert.Equal(0, role.Unit.Armor);
            Assert.True(game.ExecuteAction(WolfBite.New(1, target)));
            Assert.Equal(2, role.Unit.Health);
            Assert.Equal(4, clone.Unit.Health);
        }

        [Fact]
        public static void ViewClone_OwnsEveryActionArrayAndPreservesArmorSnapshot()
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            Board.Node target = game.Board.At(2, 3)!.Value;
            HeavyUnitView view = HeavyUnitView.New(
                Box.New(role.Unit.GetView(game).CanMoveGrids),
                Box.New(new[] { target }), Box.New(new[] { target }), Box.New(new[] { target }),
                HeavyConsts.HeavyInitialArmor, Box.New(Array.Empty<Board.Node>()),
                Box.New(Array.Empty<Board.Node>()), isCharging: false)!;
            HeavyUnitView clone = Assert.IsType<HeavyUnitView>(view.Clone());
            var originalMove = view.CanMoveGrids[0];
            clone.CanMoveGrids[0] = (role.Unit.Position, 0, null);
            clone.CanBuildFortGrids[0] = role.Unit.Position;
            clone.CanBuildWallGrids[0] = role.Unit.Position;
            clone.CanDemolishGrids[0] = role.Unit.Position;
            Assert.Equal(originalMove, view.CanMoveGrids[0]);
            Assert.Equal(target, view.CanBuildFortGrids[0]);
            Assert.Equal(target, view.CanBuildWallGrids[0]);
            Assert.Equal(target, view.CanDemolishGrids[0]);
            Assert.Equal(view.Armor, clone.Armor);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void EndAndSurrender_ClearAvailableActions(bool surrender)
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            ExecuteRoleAction(game, surrender ? GeneralSurrender.New(0) : GeneralEnd.New(0));
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
        }

        [Fact]
        public static void Heavy_RejectsOtherRolesAttacksAndSkills()
        {
            Game game = MakeGame();
            Heavy role = Role(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            GameAction[] actions =
            {
                WolfBite.New(0, target), SniperExecute.New(0, target), AlphaAttack.New(0, target),
                AlphaBlood.New(0), InkAttack.New(0, target), InkDash.New(0, target),
                ArmigerArmorUp.New(0), ArmigerThrust.New(0, target),
            };
            Assert.All(actions, action =>
            {
                Assert.False(role.Unit.HasAction(action));
                Assert.False(role.JudgeAction(action, game));
                Assert.False(game.ExecuteAction(action));
            });
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
        }
    }
}
