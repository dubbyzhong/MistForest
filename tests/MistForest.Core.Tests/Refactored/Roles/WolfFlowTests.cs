using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class WolfFlowTests
    {
        private static Game MakeWolfGame(params (int row, int col)[] positions)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));

            List<PlayerBuilder> players = positions
                .Select(position =>
                    New(
                        new List<RoleBuilder>
                        {
                            WolfBuilder.New(
                                new UnitBuilder[] { new WolfUnitBuilder() },
                                board.At(position.row, position.col)!.Value
                            )!,
                        }
                    )!
                )
                .ToList();
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        [Fact]
        public static void DemolishView_UnsupportedAction_HasNoTargetsInBaseViewOrClone()
        {
            Game game = MakeWolfGame((3, 3), (7, 7));
            Unit unit = game.GetUnit(0)!;
            Board.Node target = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(target)!);
            UnitView view = unit.GetView(game);
            GeneralDemolish demolish = GeneralDemolish.New(unit.Id, target);

            Assert.Empty(view.CanDemolishGrids);
            Assert.Empty(view.Clone().CanDemolishGrids);
            Assert.False(unit.HasAction(demolish));
            Assert.False(game.ExecuteAction(demolish));
            Assert.Empty(game.Events);
            Assert.True(unit.IsFirstAction);
            Assert.Equal(2, game.Buildings[0].Health);
        }

        private static Game MakeWolfGameWithAllyOnEnemySquare()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));

            List<PlayerBuilder> players = new()
            {
                New(
                    new List<RoleBuilder>
                    {
                        WolfBuilder.New(
                            new UnitBuilder[] { new WolfUnitBuilder() },
                            board.At(3, 2)!.Value
                        )!,
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(3, 3)!.Value
                        )!,
                    }
                )!,
                New(
                    new List<RoleBuilder>
                    {
                        WolfBuilder.New(
                            new UnitBuilder[] { new WolfUnitBuilder() },
                            board.At(3, 3)!.Value
                        )!,
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(7, 7)!.Value
                        )!,
                    }
                )!,
            };

            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        [Fact]
        public static void WolfBuilder_CreatesUnitWithStatsPositionAndClone()
        {
            Game game = MakeWolfGame((2, 2), (7, 7));
            Wolf wolf = Assert.IsType<Wolf>(game.Roles[0]);
            Wolf clone = Assert.IsType<Wolf>(wolf.Clone());

            Assert.Equal(0, wolf.Id);
            Assert.Equal(0, wolf.PlayerId);
            Assert.Equal(WolfConsts.WolfHealth, wolf.Unit.Health);
            Assert.Equal(WolfConsts.WolfAttackRange, wolf.Unit.AttackRange);
            Assert.Equal(WolfConsts.WolfMaxAction, wolf.Unit.MaxAction);
            Assert.Equal(WolfConsts.WolfBiteDamage, wolf.Unit.BiteDamage);
            Assert.Equal(game.Board.At(2, 2)!.Value, wolf.Unit.Position);
            Assert.NotSame(wolf.Unit, clone.Unit);
            Assert.Equal(wolf.Unit.Health, clone.Unit.Health);
            Assert.Equal(wolf.Unit.Position, clone.Unit.Position);
        }

        [Fact]
        public static void WolfBite_DamagesEveryEnemyOnTargetAndAggregatesVictimsIntoOneEvent()
        {
            Game game = MakeWolfGame((3, 2), (3, 3), (3, 3));
            Board.Node target = game.Board.At(3, 3)!.Value;

            Assert.True(game.ExecuteAction(WolfBite.New(callerUnitId: 0, target)));

            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(0)!.Health);
            Assert.Equal(
                WolfConsts.WolfHealth - WolfConsts.WolfBiteDamage,
                game.GetUnit(1)!.Health
            );
            Assert.Equal(
                WolfConsts.WolfHealth - WolfConsts.WolfBiteDamage,
                game.GetUnit(2)!.Health
            );
            WolfBiteEvent biteEvent = Assert.IsType<WolfBiteEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { 1, 2 }, biteEvent.UnitIds);
            Assert.Equal(0, biteEvent.AttackerId);
            Assert.Null(biteEvent.BuildingId);
            Assert.Equal(target, biteEvent.Target);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.Roles[0].HasActionLeft(game));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void WolfBite_BuildingsOfEitherOwnerRemainInvalidTargets(int owner)
        {
            Game game = MakeWolfGame((3, 3), (3, 4));
            Unit enemy = game.GetUnit(1)!;
            Board.Node target = enemy.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(owner)!.SetHealth(1)!.SetPos(target)!);

            Assert.DoesNotContain(target, ((WolfUnitView)game.GetUnit(0)!.GetView(game)).CanBiteGrids);
            Assert.False(game.ExecuteAction(WolfBite.New(0, target)));
            Assert.Empty(game.Events);
            Assert.Equal(1, game.Buildings[0].Health);
            Assert.Equal(0, game.Buildings[0].HighestDirectDamage);
            Assert.Equal(WolfConsts.WolfHealth, enemy.Health);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void WolfBite_EmptyTarget_RecordsOneEventWithNoVictimsOrBuilding()
        {
            Game game = MakeWolfGame((3, 2), (7, 7));
            Board.Node target = game.Board.At(3, 3)!.Value;

            Assert.True(game.ExecuteAction(WolfBite.New(0, target)));

            WolfBiteEvent biteEvent = Assert.IsType<WolfBiteEvent>(Assert.Single(game.Events));
            Assert.Equal(0, biteEvent.AttackerId);
            Assert.Empty(biteEvent.UnitIds);
            Assert.Null(biteEvent.BuildingId);
            Assert.Equal(target, biteEvent.Target);
        }

        [Fact]
        public static void WolfBite_DoesNotDamageAlliesSharingTheTargetSquare()
        {
            Game game = MakeWolfGameWithAllyOnEnemySquare();

            Assert.True(game.ExecuteAction(WolfBite.New(callerUnitId: 0, game.Board.At(3, 3)!.Value)));

            Assert.Equal(SniperConsts.SniperHealth, game.GetUnit(1)!.Health);
            Assert.Equal(
                WolfConsts.WolfHealth - WolfConsts.WolfBiteDamage,
                game.GetUnit(2)!.Health
            );
            WolfBiteEvent biteEvent = Assert.IsType<WolfBiteEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { 2 }, biteEvent.UnitIds);
            Assert.Equal(0, biteEvent.AttackerId);
            Assert.Null(biteEvent.BuildingId);
            Assert.Equal(game.Board.At(3, 3)!.Value, biteEvent.Target);
        }

        [Fact]
        public static void WolfBite_RejectsOutOfRangeAndWallBlockedTargets()
        {
            Game game = MakeWolfGame((3, 3), (3, 4));
            Board board = game.Board;
            Wolf roleUnit = Assert.IsType<Wolf>(game.Roles[0]);
            Board.Node adjacentTarget = board.At(3, 4)!.Value;
            Board.Node farTarget = board.At(3, 5)!.Value;

            Assert.True(roleUnit.JudgeAction(WolfBite.New(0, adjacentTarget), game));
            Assert.False(roleUnit.JudgeAction(WolfBite.New(0, farTarget), game));

            int wallIndex = board.VWallAt(board.At(3, 3)!.Value)!.Value;
            Assert.True(
                board.VerticalWalls.SetInner(
                    wallIndex / 64,
                    1UL << (wallIndex % 64),
                    out string? error
                )
            );
            Assert.Null(error);
            Assert.False(roleUnit.JudgeAction(WolfBite.New(0, adjacentTarget), game));
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void WolfBiteTargets_IncludeWhirlpoolsAndExcludeBlockedSquares()
        {
            Game game = MakeWolfGame((3, 3), (7, 7));
            Board board = game.Board;
            Board.Node lake = board.At(2, 3)!.Value;
            Board.Node wall = board.At(4, 3)!.Value;
            Board.Node whirlpool = board.At(3, 2)!.Value;
            Board.Node fort = board.At(3, 4)!.Value;
            Board.Node beyondRange = board.At(3, 5)!.Value;

            Assert.True(board.SetTerrain(lake, Terrain.Lake));
            Assert.True(board.SetTerrain(wall, Terrain.Wall));
            Assert.True(board.SetTerrain(whirlpool, Terrain.Whirlpool));
            game.AddBuilding(
                FortBuilder.New().SetHealth(1)!.SetOwner(1)!.SetPos(fort)!
            );

            WolfUnitView view = Assert.IsType<WolfUnitView>(game.GetUnit(0)!.GetView(game));

            Assert.Contains(whirlpool, view.CanBiteGrids);
            Assert.DoesNotContain(lake, view.CanBiteGrids);
            Assert.DoesNotContain(wall, view.CanBiteGrids);
            Assert.DoesNotContain(fort, view.CanBiteGrids);
            Assert.DoesNotContain(beyondRange, view.CanBiteGrids);
            Assert.False(game.ExecuteAction(WolfBite.New(0, fort)));
            Assert.False(game.ExecuteAction(GeneralDemolish.New(0, fort)));
            Assert.Equal(1, game.Buildings[0].Health);
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void WolfBite_RepeatedWithSameStrengthDoesNotStackWithinRound()
        {
            Game game = MakeWolfGame((3, 2), (3, 3));
            Board.Node target = game.Board.At(3, 3)!.Value;

            Assert.True(game.ExecuteAction(WolfBite.New(0, target)));
            int healthAfterFirstBite = game.GetUnit(1)!.Health;
            Assert.True(game.ExecuteAction(WolfBite.New(0, target)));

            Assert.Equal(WolfConsts.WolfHealth - WolfConsts.WolfBiteDamage, healthAfterFirstBite);
            Assert.Equal(healthAfterFirstBite, game.GetUnit(1)!.Health);
            Assert.Equal(2, game.Events.Count);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Wolf_MovementAndBitesShareThreeActionBudget()
        {
            Game game = MakeWolfGame((3, 2), (3, 3), (2, 1));

            Assert.True(game.ExecuteAction(WolfBite.New(0, game.Board.At(3, 3)!.Value)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 1)!.Value)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(WolfBite.New(0, game.Board.At(2, 1)!.Value)));

            Assert.Equal(new[] { 0 }, game.Board.GetGrid(game.Board.At(3, 1)!.Value)!.Units);
            Assert.False(game.Roles[0].HasActionLeft(game));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(2, game.Events.OfType<WolfBiteEvent>().Count());

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(2, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.GetUnit(0)!.IsFirstAction);
            Assert.True(game.Roles[0].HasActionLeft(game));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));
        }

        [Fact]
        public static void WolfMove_ReachableMovesExcludeCurrentSquareButPathIncludesIt()
        {
            Game game = MakeWolfGame((3, 3), (7, 7));
            Board board = game.Board;
            Board.Node start = board.At(3, 3)!.Value;
            Board.Node middle = board.At(4, 3)!.Value;
            Board.Node target = board.At(5, 3)!.Value;
            WolfUnitView view = Assert.IsType<WolfUnitView>(game.GetUnit(0)!.GetView(game));

            Assert.DoesNotContain(view.CanMoveGrids, entry => entry.node == start);
            Assert.Contains(view.CanMoveGrids, entry => entry.node == target);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));

            GeneralMoveEvent moveEvent = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { start, middle, target }, moveEvent.Path);
            Assert.Equal(target, game.GetUnit(0)!.Position);
        }

        [Fact]
        public static void Wolf_HasActionLeftWhenOnlyOneMoveDestinationRemains()
        {
            Game game = MakeWolfGame((3, 3), (7, 7));
            Board board = game.Board;
            Board.Node start = board.At(3, 3)!.Value;
            Board.Node target = board.At(3, 4)!.Value;
            foreach (Board.Node lake in new[]
            {
                board.At(2, 3)!.Value,
                board.At(4, 3)!.Value,
                board.At(3, 2)!.Value,
                board.At(2, 4)!.Value,
                board.At(4, 4)!.Value,
                board.At(3, 5)!.Value,
            })
            {
                Assert.True(board.SetTerrain(lake, Terrain.Lake));
            }
            game.AddBuilding(FortBuilder.New().SetHealth(1)!.SetOwner(0)!.SetPos(start)!);
            game.AddBuilding(FortBuilder.New().SetHealth(1)!.SetOwner(0)!.SetPos(target)!);

            WolfUnit wolf = Assert.IsType<WolfUnit>(game.GetUnit(0));
            WolfUnitView view = Assert.IsType<WolfUnitView>(wolf.GetView(game));
            Assert.Single(view.CanMoveGrids);
            Assert.Empty(view.CanBiteGrids);
            Assert.True(wolf.HasActionLeft(game));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));

            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(wolf.HasActionLeft(game));
        }
    }
}
