using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class HeavyChargeTests
    {
        private static Game MakeGame(params (int row, int col)[] enemies)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board));
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder>
                {
                    HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, board.At(4, 3)!.Value)!,
                })!,
            };
            if (enemies.Length == 0)
            {
                enemies = new[] { (7, 7) };
            }
            foreach (var enemy in enemies)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(enemy.row, enemy.col)!.Value)!,
                })!);
            }
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        private static Heavy Role(Game game) => Assert.IsType<Heavy>(game.Roles[0]);
        private static Board.Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static HeavyUnitView View(Game game) => Assert.IsType<HeavyUnitView>(Role(game).Unit.GetView(game));
        private static void Start(Game game) => Assert.True(game.ExecuteAction(HeavyChargeStart.New(0, At(game, 4, 4))));

        [Fact]
        public static void Start_MovesWithoutDamageAndWaitsForSecondSelectionPrivately()
        {
            Game game = MakeGame((4, 4));
            Heavy role = Role(game);
            Assert.Empty(View(game).CanChargeContinueGrids);
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 5))));
            Start(game);

            Assert.Equal(At(game, 4, 4), role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(At(game, 4, 3))!.Units);
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.False(role.IsFirstAction);
            Assert.True(role.HasActionLeft(game));
            Assert.True(role.Unit.IsCharging);
            Assert.True(View(game).IsCharging);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanBuildFortGrids);
            Assert.Empty(View(game).CanBuildWallGrids);
            Assert.Empty(View(game).CanDemolishGrids);
            Assert.Empty(View(game).CanChargeStartGrids);
            Assert.NotEmpty(View(game).CanChargeContinueGrids);
            HeavyChargeMoveEvent moved = Assert.IsType<HeavyChargeMoveEvent>(Assert.Single(game.GetEventsForPlayer(0)));
            Assert.Equal(new[] { At(game, 4, 3), At(game, 4, 4) }, moved.Path);
            Assert.Empty(game.GetEventsForPlayer(1));
            Assert.Empty(game.Events.OfType<GeneralMoveEvent>());
        }

        [Theory]
        [InlineData(4, 5)]
        [InlineData(4, 7)]
        [InlineData(1, 4)]
        [InlineData(7, 4)]
        public static void Continue_AllowsStraightOrSingleTurnAndEndsTurn(int row, int col)
        {
            Game game = MakeGame();
            Start(game);
            Board.Node target = At(game, row, col);
            Assert.Contains(target, View(game).CanChargeContinueGrids);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, target)));
            Assert.Equal(target, Role(game).Unit.Position);
            Assert.False(Role(game).Unit.IsCharging);
            Assert.True(Role(game).Unit.End);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 6))));
            HeavyChargeEvent hit = Assert.Single(game.Events.OfType<HeavyChargeEvent>());
            Assert.Equal(Math.Abs(row - 4) + Math.Abs(col - 4), hit.HitNodes.Length);
            Assert.Equal(hit.HitNodes.OrderBy(node => game.Board.Encode(node)), hit.HitNodes);
        }

        [Theory]
        [InlineData(4, 3)]
        [InlineData(4, 2)]
        [InlineData(3, 5)]
        [InlineData(4, 4)]
        [InlineData(1, 1)]
        public static void InvalidContinuation_DoesNotConsumeSecondSelection(int row, int col)
        {
            Game game = MakeGame();
            Start(game);
            int eventsBefore = game.Events.Count;
            Board.Node target = At(game, row, col);
            Assert.DoesNotContain(target, View(game).CanChargeContinueGrids);
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, target)));
            Assert.True(Role(game).Unit.IsCharging);
            Assert.Equal(At(game, 4, 4), Role(game).Unit.Position);
            Assert.Equal(eventsBefore, game.Events.Count);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 5))));
        }

        [Fact]
        public static void Continue_RejectsMoreThanThreeRemainingStepsAndForeignNode()
        {
            Game game = MakeGame();
            // First step up leaves four or more available board squares below it.
            Assert.True(game.ExecuteAction(HeavyChargeStart.New(0, At(game, 3, 3))));
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 3, 7))));
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, Board.New(9, 9).At(9, 9)!.Value)));
            Assert.True(Role(game).Unit.IsCharging);
        }

        [Theory]
        [InlineData("lake")]
        [InlineData("wall")]
        [InlineData("line-wall")]
        public static void BlockedContinuation_RejectsWholeRequestBeforeMovingOrDamaging(string obstruction)
        {
            Game game = MakeGame((4, 5));
            Start(game);
            Board.Node blocked = At(game, 4, 6);
            if (obstruction == "line-wall")
            {
                int wall = game.Board.VWallAt(At(game, 4, 5))!.Value;
                game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _);
            }
            else
            {
                game.Board.SetTerrain(blocked, obstruction == "lake" ? Terrain.Lake : Terrain.Wall);
            }
            Assert.False(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(At(game, 4, 4), Role(game).Unit.Position);
            Assert.True(Role(game).Unit.IsCharging);
            Assert.Single(game.Events);
        }

        [Fact]
        public static void Continue_DamagesIntermediateEnemiesAndOnlyPublishesSortedHitCoordinates()
        {
            Game game = MakeGame((3, 4), (2, 4));
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 1, 4))));
            Assert.Equal(1, game.GetUnit(1)!.Health);
            Assert.Equal(1, game.GetUnit(2)!.Health);
            HeavyChargeEvent hit = Assert.IsType<HeavyChargeEvent>(Assert.Single(game.GetEventsForPlayer(1)));
            Assert.Equal(new[] { At(game, 1, 4), At(game, 2, 4), At(game, 3, 4) }, hit.HitNodes);
            Assert.DoesNotContain(At(game, 4, 4), hit.HitNodes);
            Assert.Equal(3, game.GetEventsForPlayer(0).Length);
            Assert.Empty(game.Events.OfType<GeneralMoveEvent>());
            Assert.Empty(game.Events.OfType<AttackEvent>());
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(4, false)]
        public static void BuildingImpact_StopsAndProtectsUnitsWhetherDestroyedOrSurviving(int health, bool destroyed)
        {
            Game game = MakeGame((4, 5), (4, 6));
            Board.Node buildingPos = At(game, 4, 5);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(health)!.SetPos(buildingPos)!);
            Start(game);
            Assert.Contains(At(game, 4, 7), View(game).CanChargeContinueGrids);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(destroyed ? buildingPos : At(game, 4, 4), Role(game).Unit.Position);
            Assert.Equal(destroyed, game.Buildings[0] is Building.Rubble);
            if (!destroyed)
            {
                Assert.Equal(2, game.Buildings[0].Health);
                Assert.Equal(2, game.Buildings[0].HighestDirectDamage);
            }
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(3, game.GetUnit(2)!.Health);
            Assert.Equal(new[] { buildingPos }, Assert.Single(game.Events.OfType<HeavyChargeEvent>()).HitNodes);
        }

        [Fact]
        public static void FirstStep_CannotAttackEnemyBuildingsButCanEnterAlliedFort()
        {
            Game game = MakeGame();
            Board.Node target = At(game, 4, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2)!.SetPos(target)!);
            Assert.DoesNotContain(target, View(game).CanChargeStartGrids);
            Assert.False(game.ExecuteAction(HeavyChargeStart.New(0, target)));
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.True(Role(game).IsFirstAction);
            game.Buildings[0].Owner = 0;
            Start(game);
            Assert.Equal(2, game.Buildings[0].Health);
        }

        [Fact]
        public static void AlliedFort_PassesWithoutDamageOrStoppingAndShieldsUnits()
        {
            Game game = MakeGame((4, 5));
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(At(game, 4, 5))!);
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(At(game, 4, 7), Role(game).Unit.Position);
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.Equal(3, game.GetUnit(1)!.Health);
        }

        [Fact]
        public static void WallImpact_DestroysWallAndDoesNotContinueIntoNextEnemy()
        {
            Game game = MakeGame((4, 6));
            game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(At(game, 4, 5))!);
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(At(game, 4, 5), Role(game).Unit.Position);
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(3, game.GetUnit(1)!.Health);
        }

        [Fact]
        public static void Charging_RejectsOrdinaryActionsAndAllowsEarlyEnd()
        {
            Game game = MakeGame();
            Start(game);
            Board.Node target = At(game, 4, 5);
            GameAction[] actions =
            {
                GeneralMove.New(0, target), GeneralBuildFort.New(0, target),
                GeneralBuildWall.New(0, new[] { target })!, GeneralDemolish.New(0, target),
                HeavyChargeStart.New(0, target),
            };
            Assert.All(actions, action => Assert.False(game.ExecuteAction(action)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(At(game, 4, 4), Role(game).Unit.Position);
            Assert.Empty(Assert.Single(game.Events.OfType<HeavyChargeEvent>()).HitNodes);
            Assert.False(Role(game).Unit.IsCharging);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void NormalMovement_PreventsChargeAndGameEntrySupportsGeneralActions()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, 4))));
            Assert.Empty(View(game).CanChargeStartGrids);
            Assert.False(game.ExecuteAction(HeavyChargeStart.New(0, At(game, 4, 5))));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, 5))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.NotEmpty(View(game).CanChargeStartGrids);
        }

        [Fact]
        public static void Clone_PreservesChargingDirectionAndViewArraysAndRoundResetClearsIt()
        {
            Game game = MakeGame();
            Start(game);
            Heavy original = Role(game);
            Heavy clone = Assert.IsType<Heavy>(original.Clone());
            HeavyUnitView view = View(game);
            HeavyUnitView copy = Assert.IsType<HeavyUnitView>(view.Clone());
            Assert.True(clone.Unit.IsCharging);
            Assert.True(copy.IsCharging);
            Assert.True(clone.JudgeAction(HeavyChargeContinue.New(0, At(game, 1, 4)), game));
            Assert.False(clone.JudgeAction(HeavyChargeContinue.New(0, At(game, 4, 3)), game));
            Board.Node first = view.CanChargeContinueGrids[0];
            copy.CanChargeContinueGrids[0] = At(game, 4, 4);
            Assert.Equal(first, view.CanChargeContinueGrids[0]);
            clone.Unit.EndRound(game);
            Assert.False(clone.Unit.IsCharging);
            Assert.True(original.Unit.IsCharging);
            Assert.NotEmpty(((HeavyUnitView)clone.Unit.GetView(game)).CanChargeStartGrids);
        }

        [Fact]
        public static void EndWhileTrappedAfterFirstStep_DoesNotPrematurelySwitchPlayer()
        {
            Game game = MakeGame();
            foreach (var point in new[] { (3, 4), (5, 4), (4, 5) })
            {
                game.Board.SetTerrain(At(game, point.Item1, point.Item2), Terrain.Wall);
            }
            Start(game);
            Assert.Empty(View(game).CanChargeContinueGrids);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData("lake")]
        [InlineData("wall")]
        [InlineData("line-wall")]
        [InlineData("self")]
        [InlineData("two-steps")]
        public static void InvalidFirstStep_DoesNotMoveOrLockRole(string obstruction)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 4, 4);
            switch (obstruction)
            {
                case "lake": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "self": target = At(game, 4, 3); break;
                case "two-steps": target = At(game, 4, 5); break;
                case "line-wall":
                    int wall = game.Board.VWallAt(At(game, 4, 3))!.Value;
                    game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _);
                    break;
            }
            Assert.DoesNotContain(target, View(game).CanChargeStartGrids);
            Assert.False(game.ExecuteAction(HeavyChargeStart.New(0, target)));
            Assert.True(Role(game).IsFirstAction);
            Assert.False(Role(game).Unit.IsCharging);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void Charge_LocksRoleAndDoesNotDamageAnotherAlliedRoleOnItsPath()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, board.At(4, 3)!.Value)!,
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(4, 5)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                    SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, board.At(7, 6)!.Value)!,
                })!,
            })));
            Game game = builder.Build()!;
            Start(game);
            Assert.False(game.ExecuteAction(WolfBite.New(1, At(game, 4, 6))));
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
        }

        [Fact]
        public static void GreatWallAndRubble_ArePassedWithoutStoppingCharge()
        {
            Game game = MakeGame();
            Board.Node wall = At(game, 4, 5);
            game.Buildings.Add(new Building.GreatWall { Id = 0, Pos = wall, Owner = 0, Health = 2 });
            game.Board.GetGrid(wall)!.GridBuilding = 0;
            Board.Node rubble = At(game, 4, 6);
            game.Buildings.Add(Building.Rubble.New(1, rubble, owner: 1));
            game.Board.GetGrid(rubble)!.GridBuilding = 1;
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 7))));
            Assert.Equal(At(game, 4, 7), Role(game).Unit.Position);
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.Equal(3, Assert.Single(game.Events.OfType<HeavyChargeEvent>()).HitNodes.Length);
        }

        [Fact]
        public static void CompletedCharge_ResetsNextRoundAndSurrenderCanEndEitherStage()
        {
            Game game = MakeGame();
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, At(game, 4, 5))));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(Role(game).IsFirstAction);
            Assert.False(Role(game).Unit.IsCharging);
            Assert.NotEmpty(View(game).CanMoveGrids);
            Assert.NotEmpty(View(game).CanChargeStartGrids);
            Assert.Empty(View(game).CanChargeContinueGrids);
            Assert.True(game.ExecuteAction(HeavyChargeStart.New(0, At(game, 3, 5))));
            Assert.True(game.ExecuteAction(GeneralSurrender.New(0)));
            Assert.False(Role(game).Alive);
            Assert.False(Role(game).Unit.IsCharging);
        }

        [Fact]
        public static void CoveredBuildingDamage_DoesNotDestroyOrAllowEntry()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, board.At(4, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(4, 6)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            })));
            builder.SetFirstPlayerId(1);
            Game game = builder.Build()!;
            Board.Node target = At(game, 4, 5);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(4)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(AlphaAttack.New(1, target)));
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Start(game);
            Assert.True(game.ExecuteAction(HeavyChargeContinue.New(0, target)));
            Assert.IsType<Building.Fort>(game.Buildings[0]);
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.Equal(At(game, 4, 4), Role(game).Unit.Position);
        }

        [Fact]
        public static void ValidatedRoute_IsNotReplacedByViewQueriesAndIsConsumedOnExecution()
        {
            Game game = MakeGame((3, 4));
            Start(game);
            Heavy role = Role(game);
            HeavyChargeContinue charge = HeavyChargeContinue.New(0, At(game, 1, 4));
            Assert.True(role.JudgeAction(charge, game));

            // View generation enumerates other routes after the selected one.
            Assert.Contains(At(game, 7, 4), View(game).CanChargeContinueGrids);
            Assert.Contains(At(game, 4, 7), View(game).CanChargeContinueGrids);
            role.ExecuteAction(charge, game);

            Assert.Equal(At(game, 1, 4), role.Unit.Position);
            Assert.Equal(1, game.GetUnit(1)!.Health);
            int eventCount = game.Events.Count;
            Assert.Throws<InvalidOperationException>(() => role.ExecuteAction(charge, game));
            Assert.Equal(eventCount, game.Events.Count);
            Assert.Equal(1, game.GetUnit(1)!.Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void FailedValidation_DiscardsPreviouslyValidatedRoute(bool wrongActionType)
        {
            Game game = MakeGame();
            Start(game);
            Heavy role = Role(game);
            HeavyChargeContinue charge = HeavyChargeContinue.New(0, At(game, 1, 4));
            Assert.True(role.JudgeAction(charge, game));
            GameAction invalid = wrongActionType
                ? GeneralMove.New(0, At(game, 4, 5))
                : HeavyChargeContinue.New(0, At(game, 4, 3));
            Assert.False(role.JudgeAction(invalid, game));
            Assert.Throws<InvalidOperationException>(() => role.ExecuteAction(charge, game));
            Assert.True(role.Unit.IsCharging);
            Assert.Equal(At(game, 4, 4), role.Unit.Position);
            Assert.Single(game.Events);

            Assert.True(game.ExecuteAction(charge));
            Assert.Equal(At(game, 1, 4), role.Unit.Position);
        }

        [Fact]
        public static void DifferentTarget_CannotExecuteUsingAnotherTargetsCachedRoute()
        {
            Game game = MakeGame();
            Start(game);
            Heavy role = Role(game);
            Assert.True(role.JudgeAction(HeavyChargeContinue.New(0, At(game, 1, 4)), game));
            HeavyChargeContinue other = HeavyChargeContinue.New(0, At(game, 4, 7));
            Assert.Throws<InvalidOperationException>(() => role.ExecuteAction(other, game));
            Assert.Equal(At(game, 4, 4), role.Unit.Position);
            Assert.Single(game.Events);
            Assert.True(game.ExecuteAction(other));
            Assert.Equal(At(game, 4, 7), role.Unit.Position);
        }

        [Fact]
        public static void Clone_PreservesChargeButRequiresItsOwnValidation()
        {
            Game game = MakeGame();
            Start(game);
            Heavy original = Role(game);
            HeavyChargeContinue charge = HeavyChargeContinue.New(0, At(game, 1, 4));
            Assert.True(original.JudgeAction(charge, game));
            Heavy clone = Assert.IsType<Heavy>(original.Clone());

            Assert.True(clone.Unit.IsCharging);
            Assert.Throws<InvalidOperationException>(() => clone.ExecuteAction(charge, game));
            Assert.Equal(At(game, 4, 4), clone.Unit.Position);
            Assert.Single(game.Events);
            Assert.True(clone.JudgeAction(charge, game));

            // Validating the clone must not consume the original's cached route.
            original.ExecuteAction(charge, game);
            Assert.Equal(At(game, 1, 4), original.Unit.Position);
            Assert.Equal(At(game, 4, 4), clone.Unit.Position);
        }

        [Fact]
        public static void EndAndRoundReset_RequireFreshValidationForTheNextCharge()
        {
            Game game = MakeGame();
            Start(game);
            Heavy role = Role(game);
            HeavyChargeContinue charge = HeavyChargeContinue.New(0, At(game, 1, 4));
            Assert.True(role.JudgeAction(charge, game));
            role.ExecuteAction(GeneralEnd.New(0), game);
            role.Unit.EndRound(game);

            // Return to the same first-step position, so an old route would otherwise fit.
            HeavyChargeStart left = HeavyChargeStart.New(0, At(game, 4, 3));
            Assert.True(role.JudgeAction(left, game));
            role.ExecuteAction(left, game);
            role.Unit.EndRound(game);
            Start(game);
            Assert.Throws<InvalidOperationException>(() => role.ExecuteAction(charge, game));
            Assert.True(game.ExecuteAction(charge));
            Assert.Equal(At(game, 1, 4), role.Unit.Position);
        }
    }
}
