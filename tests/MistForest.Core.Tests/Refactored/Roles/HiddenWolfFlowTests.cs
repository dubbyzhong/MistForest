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
    public class HiddenWolfFlowTests
    {
        private static Game MakeGame(int firstPlayerId = 0)
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
                    HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() },
                        board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(3, 5)!.Value)!,
                })!,
            })));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static HiddenWolf Role(Game game) => Assert.IsType<HiddenWolf>(game.Roles[0]);

        private static void ExecuteGameAction(Game game, GameAction action)
        {
            HiddenWolf role = Role(game);
            Assert.True(role.Unit.HasAction(action));
            Assert.True(role.JudgeAction(action, game));
            Assert.True(game.ExecuteAction(action));
        }

        private static void AssertNoActions(HiddenWolfUnitView view)
        {
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanAttackGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
        }

        [Fact]
        public static void Builder_CreatesDefaultHumanAndIndependentClones()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            HiddenWolf clone = Assert.IsType<HiddenWolf>(role.Clone());
            HiddenWolfUnit unitClone = Assert.IsType<HiddenWolfUnit>(role.Unit.Clone());

            Assert.Equal(0, role.Id);
            Assert.Equal(0, role.PlayerId);
            Assert.Equal(role.Id, role.Unit.RoleId);
            Assert.Equal(0, role.Unit.Id);
            Assert.Equal(6, role.Unit.Health);
            Assert.Equal(0, role.Unit.Armor);
            Assert.Equal(2, role.Unit.AttackRange);
            Assert.Equal(4, role.Unit.AttackDamage);
            Assert.False(role.Unit.CanBeDetected);
            Assert.Equal(game.Board.At(3, 3)!.Value, role.Unit.Position);
            Assert.Same(role.Unit, Assert.Single(role.GetUnits().ToArray()));
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(role.Unit.Position)!.Units);
            Assert.NotSame(role.Unit, clone.Unit);
            Assert.NotSame(role.Unit, unitClone);
            Assert.Equal(role.Id, clone.Id);
            Assert.Equal(role.PlayerId, clone.PlayerId);
            Assert.Equal(role.Unit.Health, unitClone.Health);
            Assert.True(clone.IsFirstAction);
            Assert.False(clone.Unit.CanBeDetected);
        }

        [Fact]
        public static void Builder_RejectsInvalidStructureAndIncompleteOrConflictingConfiguration()
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            Assert.Null(HiddenWolfBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(HiddenWolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(HiddenWolfBuilder.New(new UnitBuilder[]
            {
                new HiddenWolfUnitBuilder(), new HiddenWolfUnitBuilder(),
            }, position));
            HiddenWolfUnitBuilder unitBuilder = new();
            HiddenWolfBuilder builder = HiddenWolfBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
            Assert.Null(builder.Build());
            Assert.Null(unitBuilder.Build());
            Assert.NotNull(builder.SetId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(builder.SetPlayerId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(unitBuilder.SetId(0));
            Assert.Null(builder.Build());
            Assert.NotNull(unitBuilder.SetRoleId(0));
            HiddenWolf role = Assert.IsType<HiddenWolf>(builder.Build());
            Assert.Same(role, builder.Build());
            Assert.Same(role.Unit, unitBuilder.Build());

            unitBuilder = new();
            unitBuilder.SetId(0)!.SetRoleId(0)!.SetPosition(board.At(1, 1)!.Value);
            builder = HiddenWolfBuilder.New(new UnitBuilder[] { unitBuilder }, position)!;
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
            HiddenWolfBuilder builder = HiddenWolfBuilder.New(
                new UnitBuilder[] { new HiddenWolfUnitBuilder() }, position)!;
            Assert.Equal(allowed, builder.CanInitialStay(board));
            Assert.False(builder.CanInitialStay(Board.New(1, 1)));
        }

        [Fact]
        public static void HumanMove_ConsumesOnlyActionAndUpdatesBoardAndPath()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Board.Node from = role.Unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            ExecuteGameAction(game, GeneralMove.New(0, target));

            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(target)!.Units);
            Assert.Equal(new[] { from, target },
                Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events)).Path);
            Assert.False(role.IsFirstAction);
            Assert.False(role.HasActionLeft(game));
            Assert.Equal(1, game.CurrentPlayerId);
            AssertNoActions(Assert.IsType<HiddenWolfUnitView>(role.Unit.GetView(game)));
            Assert.False(role.JudgeAction(GeneralMove.New(0, from), game));
            Assert.False(role.JudgeAction(HiddenWolfAttack.New(0, game.Board.At(3, 5)!.Value), game));
            Assert.False(role.JudgeAction(GeneralBuildFort.New(0, from), game));

            HiddenWolf clone = Assert.IsType<HiddenWolf>(role.Clone());
            Assert.False(clone.HasActionLeft(game));
            role.Unit.EndRound(game);
            Assert.True(role.IsFirstAction);
            Assert.True(role.HasActionLeft(game));
            Assert.False(clone.IsFirstAction);
            Assert.False(clone.HasActionLeft(game));
        }

        [Theory]
        [InlineData("self")]
        [InlineData("two-steps")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("line-wall")]
        [InlineData("enemy-fort")]
        [InlineData("allied-wall")]
        [InlineData("out-of-board")]
        public static void HumanMove_RejectsInvalidTargetsWithoutConsumingAction(string obstruction)
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Board.Node from = role.Unit.Position;
            Board.Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "self": target = from; break;
                case "two-steps": target = game.Board.At(3, 5)!.Value; break;
                case "lake": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "terrain-wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "line-wall":
                    int wall = game.Board.VWallAt(from)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
                case "enemy-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(4)!.SetPos(target)!);
                    break;
                case "allied-wall":
                    game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(4)!.SetPos(target)!);
                    break;
                case "out-of-board": target = Board.New(8, 8).At(8, 8)!.Value; break;
            }
            Assert.DoesNotContain(role.Unit.GetView(game).CanMoveGrids, entry => entry.node == target);
            Assert.False(role.JudgeAction(GeneralMove.New(0, target), game));
            Assert.False(game.ExecuteAction(GeneralMove.New(0, target)));
            Assert.Equal(from, role.Unit.Position);
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void HumanAttack_DamagesEnemyAtRangeTwoAndAutomaticallyEndsTurn()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Wolf enemy = Assert.IsType<Wolf>(game.Roles[1]);
            Board.Node from = role.Unit.Position;
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, enemy.Unit.Position)));
            Assert.Equal(2, enemy.Unit.Health);
            Assert.Equal(4, enemy.Unit.HighestDirectDamage);
            Assert.Equal(from, role.Unit.Position);
            Assert.False(role.HasActionLeft(game));
            Assert.False(role.IsFirstAction);
            Assert.Equal(1, game.CurrentPlayerId);
            AssertNoActions(Assert.IsType<HiddenWolfUnitView>(role.Unit.GetView(game)));
            HiddenWolfAttackEvent attack = Assert.IsType<HiddenWolfAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(0, attack.AttackerId);
            Assert.Equal(enemy.Unit.Position, attack.Target);
            Assert.Equal(new[] { 1 }, attack.UnitIds);
            Assert.Null(attack.BuildingId);
            Assert.False(role.JudgeAction(HiddenWolfAttack.New(0, enemy.Unit.Position), game));
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(2, 3)!.Value), game));

            HiddenWolf clone = Assert.IsType<HiddenWolf>(role.Clone());
            role.Unit.EndRound(game);
            Assert.True(role.HasActionLeft(game));
            Assert.False(clone.HasActionLeft(game));
        }

        [Theory]
        [InlineData("out-of-range")]
        [InlineData("terrain-wall")]
        [InlineData("lake-target")]
        [InlineData("middle-wall")]
        [InlineData("middle-fort")]
        [InlineData("line-wall")]
        [InlineData("out-of-board")]
        public static void HumanAttack_RejectsInvalidTargetsWithoutSideEffects(string obstruction)
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Board.Node target = game.Board.At(3, 5)!.Value;
            Board.Node middle = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "out-of-range": target = game.Board.At(3, 6)!.Value; break;
                case "terrain-wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "lake-target": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "middle-wall": game.Board.SetTerrain(middle, Terrain.Wall); break;
                case "middle-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(4)!.SetPos(middle)!);
                    break;
                case "line-wall":
                    int wall = game.Board.VWallAt(role.Unit.Position)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
                case "out-of-board": target = Board.New(8, 8).At(8, 8)!.Value; break;
            }
            Assert.DoesNotContain(target, Assert.IsType<HiddenWolfUnitView>(role.Unit.GetView(game)).CanAttackGrids);
            Assert.False(game.ExecuteAction(HiddenWolfAttack.New(0, target)));
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
            Assert.Equal(6, Assert.IsType<Wolf>(game.Roles[1]).Unit.Health);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void HumanAttack_CrossesLakeAndConsumesActionWhenEmpty()
        {
            Game game = MakeGame();
            game.Board.SetTerrain(game.Board.At(2, 3)!.Value, Terrain.Lake);
            Board.Node target = game.Board.At(1, 3)!.Value;
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, target)));
            HiddenWolfAttackEvent attack = Assert.IsType<HiddenWolfAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(target, attack.Target);
            Assert.Empty(attack.UnitIds);
            Assert.Null(attack.BuildingId);
            Assert.Equal(0, Role(game).Unit.HighestDirectDamage);
            Assert.Equal(0, Assert.IsType<Wolf>(game.Roles[1]).Unit.HighestDirectDamage);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(1, 4)]
        [InlineData(1, 8)]
        public static void HumanAttack_HitsBuildingAndProtectsItsOccupants(int owner, int health)
        {
            Game game = MakeGame();
            Wolf enemy = Assert.IsType<Wolf>(game.Roles[1]);
            Board.Node target = enemy.Unit.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(owner)!.SetHealth(health)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, target)));
            Assert.Equal(6, enemy.Unit.Health);
            Assert.Equal(0, enemy.Unit.HighestDirectDamage);
            if (health == 4)
            {
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
            }
            else
            {
                Assert.Equal(health - 4, Assert.IsType<Building.Fort>(game.Buildings[0]).Health);
            }
            HiddenWolfAttackEvent attack = Assert.IsType<HiddenWolfAttackEvent>(Assert.Single(game.Events));
            Assert.Empty(attack.UnitIds);
            Assert.Equal(0, attack.BuildingId);
        }

        [Fact]
        public static void HumanAttack_OnOwnGridCannotDamageItsCaster()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, role.Unit.Position)));
            Assert.Equal(6, role.Unit.Health);
            Assert.Equal(0, role.Unit.HighestDirectDamage);
            Assert.Empty(Assert.IsType<HiddenWolfAttackEvent>(Assert.Single(game.Events)).UnitIds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Build_UsesGeneralRulesAndConsumesTheWholeAction(bool wall)
        {
            Game game = MakeGame();
            Board.Node target = game.Board.At(2, 3)!.Value;
            GameAction action = wall ? GeneralBuildWall.New(0, new[] { target })!
                : GeneralBuildFort.New(0, target);
            ExecuteGameAction(game, action);
            Building building = Assert.Single(game.Buildings);
            Assert.Equal(wall ? typeof(Building.Wall) : typeof(Building.Fort), building.GetType());
            Assert.Equal(target, building.Pos);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Equal(1, game.CurrentPlayerId);
            AssertNoActions(Assert.IsType<HiddenWolfUnitView>(Role(game).Unit.GetView(game)));
        }

        [Fact]
        public static void Demolish_OnlyTargetsAlliedBuildingsAndConsumesAction()
        {
            Game game = MakeGame();
            Board.Node allied = game.Board.At(2, 3)!.Value;
            Board.Node enemy = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(4)!.SetPos(allied)!);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(4)!.SetPos(enemy)!);
            HiddenWolfUnitView view = Assert.IsType<HiddenWolfUnitView>(Role(game).Unit.GetView(game));
            Assert.Contains(allied, view.CanDemolishGrids);
            Assert.DoesNotContain(enemy, view.CanDemolishGrids);
            Assert.False(Role(game).JudgeAction(GeneralDemolish.New(0, enemy), game));
            Assert.False(game.ExecuteAction(GeneralDemolish.New(0, enemy)));
            ExecuteGameAction(game, GeneralDemolish.New(0, allied));
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.IsType<Building.Fort>(game.Buildings[1]);
            Assert.IsType<GeneralDemolishEvent>(Assert.Single(game.Events));
            Assert.Equal(1, game.CurrentPlayerId);
            AssertNoActions(Assert.IsType<HiddenWolfUnitView>(Role(game).Unit.GetView(game)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void EndAndSurrender_RemoveAllAvailableActions(bool surrender)
        {
            Game game = MakeGame();
            ExecuteGameAction(game, surrender ? GeneralSurrender.New(0) : GeneralEnd.New(0));
            Assert.Equal(!surrender, Role(game).Unit.Alive);
            Assert.False(Role(game).HasActionLeft(game));
            AssertNoActions(Assert.IsType<HiddenWolfUnitView>(Role(game).Unit.GetView(game)));
            Assert.False(Role(game).JudgeAction(HiddenWolfAttack.New(0, game.Board.At(3, 5)!.Value), game));
            if (surrender)
            {
                Assert.False(game.Players[0].Alive);
                Assert.Empty(game.Board.GetGrid(Role(game).Unit.Position)!.Units);
            }
            else
            {
                Assert.Equal(1, game.CurrentPlayerId);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void GameEntry_RestoresHumanActionsAfterAllPlayersEndTheirTurns(bool move)
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            ExecuteGameAction(game, move
                ? GeneralMove.New(0, game.Board.At(3, 4)!.Value)
                : HiddenWolfAttack.New(0, game.Board.At(1, 3)!.Value));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(role.IsFirstAction);
            Assert.False(role.HasActionLeft(game));

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.IsFirstAction);
            Assert.True(role.HasActionLeft(game));
            HiddenWolfUnitView view = Assert.IsType<HiddenWolfUnitView>(role.Unit.GetView(game));
            Assert.NotEmpty(view.CanMoveGrids);
            Assert.NotEmpty(view.CanAttackGrids);
            ExecuteGameAction(game, HiddenWolfAttack.New(0, view.CanAttackGrids[0]));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void View_OffersExactlyTheTargetsAcceptedByValidation()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            game.Board.SetTerrain(game.Board.At(2, 3)!.Value, Terrain.Wall);
            game.Board.SetTerrain(game.Board.At(4, 3)!.Value, Terrain.Lake);
            HiddenWolfUnitView view = Assert.IsType<HiddenWolfUnitView>(role.Unit.GetView(game));
            for (int row = 1; row <= game.Board.Height; ++row)
            {
                for (int col = 1; col <= game.Board.Width; ++col)
                {
                    Board.Node node = game.Board.At(row, col)!.Value;
                    Assert.Equal(view.CanMoveGrids.Any(entry => entry.node == node),
                        role.JudgeAction(GeneralMove.New(0, node), game));
                    Assert.Equal(view.CanAttackGrids.Contains(node),
                        role.JudgeAction(HiddenWolfAttack.New(0, node), game));
                }
            }
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void ViewClone_OwnsEveryActionArray()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Board.Node target = game.Board.At(2, 3)!.Value;
            HiddenWolfUnitView view = HiddenWolfUnitView.New(
                Box.New(role.Unit.GetView(game).CanMoveGrids), Box.New(new[] { target }),
                Box.New(new[] { target }), Box.New(new[] { target }), Box.New(new[] { target }))!;
            HiddenWolfUnitView clone = Assert.IsType<HiddenWolfUnitView>(view.Clone());
            var originalMove = view.CanMoveGrids[0];
            clone.CanMoveGrids[0] = (role.Unit.Position, 0, null);
            clone.CanAttackGrids[0] = role.Unit.Position;
            clone.CanBuildFortGrids[0] = role.Unit.Position;
            clone.CanBuildWallGrids[0] = role.Unit.Position;
            clone.CanDemolishGrids[0] = role.Unit.Position;
            Assert.Equal(originalMove, view.CanMoveGrids[0]);
            Assert.Equal(target, view.CanAttackGrids[0]);
            Assert.Equal(target, view.CanBuildFortGrids[0]);
            Assert.Equal(target, view.CanBuildWallGrids[0]);
            Assert.Equal(target, view.CanDemolishGrids[0]);
        }

        [Fact]
        public static void HumanForm_IsExcludedFromExistingAlphaDetection()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board))!.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, board.At(3, 4)!.Value)!,
                })!,
            }));
            Game game = builder.Build()!;
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.Null(Assert.Single(game.Events.OfType<AlphaBloodResultEvent>()).Target);
        }

        [Fact]
        public static void HiddenWolf_RejectsOtherRolesAttacksAndSkills()
        {
            Game game = MakeGame();
            HiddenWolf role = Role(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            GameAction[] actions =
            {
                WolfBite.New(0, target), SniperExecute.New(0, target), AlphaAttack.New(0, target),
                AlphaBlood.New(0), InkAttack.New(0, target), InkDash.New(0, target),
                ArmigerArmorUp.New(0), ArmigerThrust.New(0, target), HeavyChargeStart.New(0, target),
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
