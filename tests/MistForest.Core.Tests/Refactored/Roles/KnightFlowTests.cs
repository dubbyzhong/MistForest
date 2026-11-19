using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;
using static MistForest.Core.Consts.CombatConsts;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class KnightFlowTests
    {
        private static Game MakeGame(int row = 4, int col = 4)
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
                    KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, board.At(row, col)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            })));
            return builder.Build()!;
        }

        private static Knight Role(Game game) => Assert.IsType<Knight>(game.Roles[0]);
        private static KnightUnitView View(Game game) => Assert.IsType<KnightUnitView>(Role(game).Unit.GetView(game));
        private static Board.Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;

        [Fact]
        public static void Builder_InitializesKnightAndCachesAValidBuild()
        {
            Board board = Board.New(7, 7);
            Board.Node position = board.At(4, 4)!.Value;
            Assert.Null(KnightBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(KnightBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder(), new KnightUnitBuilder() }, position));
            KnightUnitBuilder unit = new();
            KnightBuilder builder = KnightBuilder.New(new UnitBuilder[] { unit }, position)!;
            Assert.Null(builder.Build());
            Assert.Null(unit.Build());
            builder.SetId(0)!.SetPlayerId(0);
            Assert.Null(builder.Build());
            unit.SetId(0)!.SetRoleId(0);
            Knight role = Assert.IsType<Knight>(builder.Build());
            Assert.Same(role, builder.Build());
            Assert.Same(role.Unit, unit.Build());
            Assert.Equal(3 * HalfPoint, role.Unit.Health);
            Assert.Equal(0, role.Unit.AttackRange);
            Assert.Equal(position, role.Unit.Position);
            Assert.Same(role.Unit, Assert.Single(role.GetUnits().ToArray()));
            KnightUnitBuilder conflicting = new();
            conflicting.SetId(0)!.SetRoleId(0)!.SetPosition(board.At(1, 1)!.Value);
            builder = KnightBuilder.New(new UnitBuilder[] { conflicting }, position)!;
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
            Board.Node target = board.At(2, 2)!.Value;
            board.SetTerrain(target, terrain);
            KnightBuilder builder = KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, target)!;
            Assert.Equal(allowed, builder.CanInitialStay(board));
            Assert.False(builder.CanInitialStay(Board.New(1, 1)));
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(2, -1)]
        [InlineData(-2, 1)]
        [InlineData(-2, -1)]
        [InlineData(1, 2)]
        [InlineData(-1, 2)]
        [InlineData(1, -2)]
        [InlineData(-1, -2)]
        public static void Leap_SupportsAllEightDirectionsAndKeepsCoordinatesPrivate(int dx, int dy)
        {
            Game game = MakeGame();
            Board.Node from = Role(game).Unit.Position;
            Board.Node target = game.Board.Move(from, dx, dy)!.Value;
            Assert.Equal(8, View(game).CanLeapGrids.Length);
            Assert.Contains(target, View(game).CanLeapGrids);
            Assert.Empty(View(game).CanMoveGrids);
            Knight originalClone = Assert.IsType<Knight>(Role(game).Clone());
            Assert.True(game.ExecuteAction(KnightLeap.New(0, target)));
            Assert.Equal(target, Role(game).Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(target)!.Units);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(Role(game).Unit.End);
            Assert.False(Role(game).IsFirstAction);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Empty(View(game).CanLeapGrids);
            Assert.Empty(View(game).CanBuildFortGrids);
            Assert.Empty(View(game).CanBuildWallGrids);
            Assert.Empty(View(game).CanDemolishGrids);
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(from, originalClone.Unit.Position);
            Assert.False(originalClone.Unit.End);
            Assert.Single(game.GetEventsForPlayer(1));
            Assert.IsType<KnightLeapEvent>(game.GetEventsForPlayer(1)[0]);
            KnightLeapResultEvent result = Assert.Single(game.GetEventsForPlayer(0).OfType<KnightLeapResultEvent>());
            Assert.Equal(from, result.From);
            Assert.Equal(target, result.Target);
            Assert.Equal(0, result.UnitId);
            Assert.Empty(game.Events.OfType<GeneralMoveEvent>());
            Knight exhausted = Assert.IsType<Knight>(Role(game).Clone());
            Assert.Empty(exhausted.Unit.GetView(game).CanMoveGrids);
            Assert.False(exhausted.HasActionLeft(game));
        }

        [Theory]
        [InlineData(1, 0, false)]
        [InlineData(-1, 0, false)]
        [InlineData(0, 1, false)]
        [InlineData(0, -1, false)]
        [InlineData(1, 0, true)]
        [InlineData(-1, 0, true)]
        [InlineData(0, 1, true)]
        [InlineData(0, -1, true)]
        public static void Leap_AWallOrStartingEdgeBlocksBothTargetsSharingALeg(int dx, int dy, bool edge)
        {
            Game game = MakeGame();
            Board.Node from = Role(game).Unit.Position;
            Board.Node leg = game.Board.Move(from, dx, dy)!.Value;
            if (edge)
            {
                Board.Node anchor = dx < 0 || dy < 0 ? leg : from;
                int index = dx != 0 ? game.Board.HWallAt(anchor)!.Value : game.Board.VWallAt(anchor)!.Value;
                LineWalls walls = dx != 0 ? game.Board.HorizontalWalls : game.Board.VerticalWalls;
                Assert.True(walls.SetInner(index / 64, 1ul << (index % 64), out _));
            }
            else game.Board.SetTerrain(leg, Terrain.Wall);
            Assert.Equal(6, View(game).CanLeapGrids.Length);
            foreach (int side in new[] { -1, 1 })
            {
                Board.Node target = game.Board.Move(from, dx * 2 + dy * side, dy * 2 + dx * side)!.Value;
                Assert.DoesNotContain(target, View(game).CanLeapGrids);
                Assert.False(game.ExecuteAction(KnightLeap.New(0, target)));
            }
            Assert.True(Role(game).IsFirstAction);
            Assert.Empty(game.Events);
        }

        [Theory]
        [InlineData("lake-leg")]
        [InlineData("building-leg")]
        [InlineData("intermediate-wall")]
        [InlineData("later-edge")]
        public static void Leap_DoesNotTreatItsJumpAsAnOrdinaryWalkingPath(string obstacle)
        {
            Game game = MakeGame();
            Board.Node leg = At(game, 5, 4);
            Board.Node target = At(game, 6, 5);
            switch (obstacle)
            {
                case "lake-leg": game.Board.SetTerrain(leg, Terrain.Lake); break;
                case "building-leg":
                    game.AddBuilding(WallBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(leg)!); break;
                case "intermediate-wall": game.Board.SetTerrain(At(game, 6, 4), Terrain.Wall); break;
                case "later-edge":
                    int index = game.Board.HWallAt(leg)!.Value;
                    Assert.True(game.Board.HorizontalWalls.SetInner(index / 64, 1ul << (index % 64), out _));
                    break;
            }
            Assert.True(game.ExecuteAction(KnightLeap.New(0, target)));
            Assert.Equal(target, Role(game).Unit.Position);
        }

        [Theory]
        [InlineData("ground", true)]
        [InlineData("whirlpool", true)]
        [InlineData("lake", false)]
        [InlineData("wall", false)]
        [InlineData("own-fort", true)]
        [InlineData("enemy-fort", false)]
        [InlineData("own-wall", false)]
        [InlineData("own-great-wall", true)]
        [InlineData("rubble", true)]
        [InlineData("enemy-unit", true)]
        public static void Leap_ValidatesTheLandingWithoutDamagingOccupants(string landing, bool allowed)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 6, 5);
            if (landing is "lake" or "wall" or "whirlpool")
                game.Board.SetTerrain(target, landing == "lake" ? Terrain.Lake : landing == "wall" ? Terrain.Wall : Terrain.Whirlpool);
            if (landing.Contains("fort") || landing == "rubble")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(landing == "enemy-fort" ? 1 : 0)!.SetHealth(UnitsPerPoint)!.SetPos(target)!);
                if (landing == "rubble") game.Buildings[0].Destroyed(game, 0);
            }
            if (landing == "own-wall") game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(target)!);
            if (landing == "own-great-wall")
            {
                game.Buildings.Add(new Building.GreatWall { Id = 0, Pos = target, Owner = 0, Health = UnitsPerPoint });
                game.Board.GetGrid(target)!.GridBuilding = 0;
            }
            if (landing == "enemy-unit")
            {
                Unit enemy = game.GetUnit(1)!;
                game.Board.GetGrid(enemy.Position)!.Units.Remove(1);
                enemy.Position = target;
                game.Board.GetGrid(target)!.Units.Add(1);
            }
            Assert.Equal(allowed, View(game).CanLeapGrids.Contains(target));
            Assert.Equal(allowed, game.ExecuteAction(KnightLeap.New(0, target)));
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            if (!allowed)
            {
                Assert.True(Role(game).IsFirstAction);
                Assert.Empty(game.Events);
            }
        }

        [Fact]
        public static void ViewAndValidation_AgreeAtTheBoardBoundaryAndCloneAllArrays()
        {
            Game game = MakeGame(1, 1);
            KnightUnitView view = View(game);
            Assert.Equal(2, view.CanLeapGrids.Length);
            for (int row = 1; row <= 7; ++row)
            for (int col = 1; col <= 7; ++col)
            {
                Board.Node target = At(game, row, col);
                Assert.Equal(view.CanLeapGrids.Contains(target), Role(game).JudgeAction(KnightLeap.New(0, target), game));
            }
            Assert.False(game.ExecuteAction(KnightLeap.New(0, Board.New(9, 9).At(9, 9)!.Value)));
            Board.Node marker = At(game, 2, 1);
            view = KnightUnitView.New(Box.New(view.CanLeapGrids), Box.New(new[] { marker }),
                Box.New(new[] { marker }), Box.New(new[] { marker }))!;
            KnightUnitView clone = Assert.IsType<KnightUnitView>(view.Clone());
            clone.CanLeapGrids[0] = marker;
            clone.CanBuildFortGrids[0] = At(game, 7, 7);
            clone.CanBuildWallGrids[0] = At(game, 7, 7);
            clone.CanDemolishGrids[0] = At(game, 7, 7);
            Assert.DoesNotContain(marker, view.CanLeapGrids);
            Assert.Equal(marker, view.CanBuildFortGrids[0]);
            Assert.Equal(marker, view.CanBuildWallGrids[0]);
            Assert.Equal(marker, view.CanDemolishGrids[0]);
        }

        [Theory]
        [InlineData("fort")]
        [InlineData("wall")]
        [InlineData("demolish")]
        [InlineData("end")]
        [InlineData("surrender")]
        public static void GeneralActions_AreAvailableAndConsumeTheTurn(string kind)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 3, 4);
            if (kind == "demolish") game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(target)!);
            GameAction action = kind switch
            {
                "fort" => GeneralBuildFort.New(0, target),
                "wall" => GeneralBuildWall.New(0, new[] { target })!,
                "demolish" => GeneralDemolish.New(0, target),
                "end" => GeneralEnd.New(0),
                _ => GeneralSurrender.New(0),
            };
            Assert.True(game.ExecuteAction(action));
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Empty(View(game).CanLeapGrids);
            if (kind != "surrender") Assert.Equal(1, game.CurrentPlayerId);
            else Assert.False(Role(game).Alive);
        }

        [Fact]
        public static void Leap_ResetsNextRoundAndRejectsNormalMovementAndOtherRolesAttacks()
        {
            Game game = MakeGame();
            Assert.False(game.ExecuteAction(GeneralMove.New(0, At(game, 4, 5))));
            Assert.False(game.ExecuteAction(WolfBite.New(0, At(game, 4, 5))));
            Assert.False(game.ExecuteAction(KnightLeap.New(1, At(game, 5, 6))));
            Assert.True(Role(game).IsFirstAction);
            Assert.True(game.ExecuteAction(KnightLeap.New(0, At(game, 6, 5))));
            Assert.False(Role(game).JudgeAction(GeneralBuildFort.New(0, At(game, 5, 5)), game));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(Role(game).IsFirstAction);
            Assert.Contains(At(game, 4, 4), View(game).CanLeapGrids);
            Assert.True(game.ExecuteAction(KnightLeap.New(0, At(game, 4, 4))));
        }
    }
}
