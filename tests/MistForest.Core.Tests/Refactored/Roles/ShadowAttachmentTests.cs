using MistForest.Core.Consts;
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
    public class ShadowAttachmentTests
    {
        private static Game MakeGame(string target = "wolf", int first = 0, bool threePlayers = false,
            int sourceRow = 3, int sourceCol = 3, int targetRow = 4, int targetCol = 4)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node position = board.At(targetRow, targetCol)!.Value;
            RoleBuilder other = target switch
            {
                "ink" => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, position)!,
                "heavy" => HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, position)!,
                "alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, position)!,
                "hidden" => HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, position)!,
                "shadow" => ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() }, position)!,
                "sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, position)!,
                "knight" => KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, position)!,
                _ => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position)!,
            };
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder>
                {
                    ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() }, board.At(sourceRow, sourceCol)!.Value)!,
                    SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    other,
                    (target == "ink"
                        ? WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(6, 6)!.Value)
                        : (RoleBuilder?)InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(6, 6)!.Value))!,
                })!,
            };
            if (threePlayers)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(6, 5)!.Value)!,
                    SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, board.At(6, 7)!.Value)!,
                })!);
            }
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode));
            builder.SetBoard(Box.New(board));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            builder.SetFirstPlayerId(first);
            return Assert.IsType<Game>(builder.Build());
        }

        private static Shadow Shadow(Game game) => Assert.IsType<Shadow>(game.Roles[0]);
        private static ShadowUnitView View(Game game) => Assert.IsType<ShadowUnitView>(Shadow(game).Unit.GetView(game));
        private static Board.Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static GameAction Link(bool drag, int target = 2) => drag ? ShadowDrag.New(0, target) : ShadowFollow.New(0, target);

        [Theory]
        [InlineData(2, 1)]
        [InlineData(2, -1)]
        [InlineData(-2, 1)]
        [InlineData(-2, -1)]
        [InlineData(1, 2)]
        [InlineData(1, -2)]
        [InlineData(-1, 2)]
        [InlineData(-1, -2)]
        public static void FollowKnight_CopiesAllEightJumpDirectionsWithoutRecursiveMovement(int dx, int dy)
        {
            Game game = MakeGame("knight");
            Board.Node from = Shadow(game).Unit.Position;
            Board.Node target = game.Board.Move(game.GetUnit(2)!.Position, dx, dy)!.Value;
            Board.Node expected = game.Board.Move(from, dx, dy)!.Value;
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Shadow clone = Assert.IsType<Shadow>(Shadow(game).Clone());
            Assert.True(game.ExecuteAction(KnightLeap.New(2, target)));
            Assert.Equal(target, game.GetUnit(2)!.Position);
            Assert.Equal(expected, Shadow(game).Unit.Position);
            Assert.Equal(from, clone.Unit.Position);
            ShadowCopyMoveEvent copied = Assert.Single(game.Events.OfType<ShadowCopyMoveEvent>());
            Assert.Equal(new[] { from, expected }, copied.Path);
            Assert.Equal(0, copied.UnitId);
            Assert.False(Assert.Single(game.Events.OfType<ShadowAttachmentEvent>()).Blocked);
            Assert.Equal(3 * HalfPoint, Shadow(game).Unit.Health);
        }

        [Theory]
        [InlineData("leap")]
        [InlineData("hunt")]
        [InlineData("secret")]
        public static void FollowKnight_UsesTheSameJumpRulesForEveryJumpAction(string kind)
        {
            Game game = MakeGame("knight");
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Board.Node target = At(game, 6, 5);
            if (kind == "secret")
                Assert.True(game.ExecuteAction(KnightHunt.New(2, game.GetUnit(2)!.Position)));
            Assert.Empty(game.Events.OfType<ShadowCopyMoveEvent>());
            GameAction action = kind switch
            {
                "leap" => KnightLeap.New(2, target),
                "hunt" => KnightHunt.New(2, target),
                _ => KnightSecretLeap.New(2, target),
            };
            Assert.True(game.ExecuteAction(action));
            Assert.Equal(At(game, 5, 4), Shadow(game).Unit.Position);
            Assert.Single(game.Events.OfType<ShadowCopyMoveEvent>());
        }

        [Theory]
        [InlineData("leg-wall", false)]
        [InlineData("leg-edge", false)]
        [InlineData("leg-lake", true)]
        [InlineData("leg-building", true)]
        [InlineData("landing-wall", false)]
        [InlineData("landing-lake", false)]
        [InlineData("enemy-fort", false)]
        [InlineData("own-fort", true)]
        [InlineData("intermediate-wall", true)]
        [InlineData("later-edge", true)]
        public static void FollowKnight_ChecksItsOwnLegAndLandingWithoutBlockingTheKnight(string obstacle, bool allowed)
        {
            Game game = MakeGame("knight", targetRow: 5, targetCol: 5);
            Board.Node from = Shadow(game).Unit.Position;
            Board.Node leg = At(game, 2, 3);
            Board.Node landing = At(game, 1, 4);
            switch (obstacle)
            {
                case "leg-wall": game.Board.SetTerrain(leg, Terrain.Wall); break;
                case "leg-lake": game.Board.SetTerrain(leg, Terrain.Lake); break;
                case "leg-building": game.AddBuilding(WallBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(leg)!); break;
                case "leg-edge":
                    int edge = game.Board.HWallAt(leg)!.Value;
                    game.Board.HorizontalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _); break;
                case "landing-wall": game.Board.SetTerrain(landing, Terrain.Wall); break;
                case "landing-lake": game.Board.SetTerrain(landing, Terrain.Lake); break;
                case "enemy-fort":
                case "own-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(obstacle == "own-fort" ? 0 : 1)!
                        .SetHealth(UnitsPerPoint)!.SetPos(landing)!); break;
                case "intermediate-wall": game.Board.SetTerrain(At(game, 1, 3), Terrain.Wall); break;
                case "later-edge":
                    edge = game.Board.HWallAt(At(game, 1, 3))!.Value;
                    game.Board.HorizontalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _); break;
            }
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(KnightLeap.New(2, At(game, 3, 6))));
            Assert.Equal(At(game, 3, 6), game.GetUnit(2)!.Position);
            Assert.Equal(allowed ? landing : from, Shadow(game).Unit.Position);
            Assert.Equal(!allowed, Assert.Single(game.Events.OfType<ShadowAttachmentEvent>()).Blocked);
            Assert.Equal(allowed ? 1 : 0, game.Events.OfType<ShadowCopyMoveEvent>().Count());
        }

        [Theory]
        [InlineData(7, 7, 2, 1, "clear", true)]
        [InlineData(1, 1, -2, -1, "clear", true)]
        [InlineData(7, 7, 2, 1, "leg-wall", false)]
        [InlineData(6, 7, 2, 1, "leg-edge", false)]
        public static void FollowKnight_WrapsTheJumpAndChecksTheLegAtTheWrappedBoundary(
            int row, int col, int dx, int dy, string obstacle, bool allowed)
        {
            Game game = MakeGame("knight", sourceRow: row, sourceCol: col);
            Board.Node from = Shadow(game).Unit.Position;
            if (obstacle == "leg-wall") game.Board.SetTerrain(At(game, 1, 7), Terrain.Wall);
            if (obstacle == "leg-edge")
            {
                int edge = game.Board.HWallAt(from)!.Value;
                game.Board.HorizontalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _);
            }
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(KnightLeap.New(2,
                game.Board.Move(game.GetUnit(2)!.Position, dx, dy)!.Value)));
            Board.Node expected = At(game, (row - 1 + dx + 7) % 7 + 1, (col - 1 + dy + 7) % 7 + 1);
            Assert.Equal(allowed ? expected : from, Shadow(game).Unit.Position);
            Assert.Equal(!allowed, Assert.Single(game.Events.OfType<ShadowAttachmentEvent>()).Blocked);
        }

        [Fact]
        public static void FollowKnight_SilenceStillPausesCopiedJumps()
        {
            Game game = MakeGame("knight");
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(KnightHuntTests.ApplySilence(Shadow(game).Unit));
            Assert.True(game.ExecuteAction(KnightLeap.New(2, At(game, 6, 5))));
            Assert.Equal(At(game, 3, 3), Shadow(game).Unit.Position);
            Assert.Empty(game.Events.OfType<ShadowCopyMoveEvent>());
            Assert.Empty(game.Events.OfType<ShadowAttachmentEvent>());
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(false, 3)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        [InlineData(true, 3)]
        public static void SelectLink_AcceptsAnyLivingUnitWithoutRangeOrAllianceLimitAndLocksRole(bool drag, int target)
        {
            Game game = MakeGame();
            Assert.Contains(target, View(game).CanFollowUnitIds);
            Assert.Contains(target, View(game).CanDragUnitIds);
            Assert.True(game.ExecuteAction(Link(drag, target)));
            Assert.True(Shadow(game).Unit.IsFirstAction);
            Assert.True(Shadow(game).HasStartedTurn);
            Assert.Equal(target, Shadow(game).Unit.TargetUnitId);
            Assert.Equal(drag ? ShadowLinkMode.Drag : ShadowLinkMode.Follow, View(game).LinkMode);
            Assert.True(View(game).HasSelectedLinkThisTurn);
            Assert.Empty(View(game).CanFollowUnitIds);
            Assert.Empty(View(game).CanDragUnitIds);
            Assert.Contains(View(game).CanMoveGrids, entry => entry.distance == 2);
            Assert.NotEmpty(View(game).CanAttackGrids);
            Assert.False(game.ExecuteAction(GeneralMove.New(1, At(game, 7, 6))));
            Assert.False(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.False(game.ExecuteAction(Link(!drag, target)));
            Assert.False(game.ExecuteAction(Link(drag, target)));
            ShadowLinkEvent selected = Assert.IsType<ShadowLinkEvent>(Assert.Single(game.Events));
            Assert.Equal(target, selected.TargetUnitId);
            Assert.Equal(0, selected.UnitId);
        }

        [Theory]
        [InlineData(false, -1)]
        [InlineData(false, 0)]
        [InlineData(false, 999)]
        [InlineData(true, -1)]
        [InlineData(true, 0)]
        [InlineData(true, 999)]
        public static void SelectLink_RejectsInvalidIdsWithoutConsumingAnything(bool drag, int target)
        {
            Game game = MakeGame();
            Assert.DoesNotContain(target, View(game).CanFollowUnitIds);
            Assert.False(game.ExecuteAction(Link(drag, target)));
            Assert.False(Shadow(game).HasStartedTurn);
            Assert.Null(Shadow(game).Unit.TargetUnitId);
            Assert.Empty(game.Events);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void SelectLink_IsRejectedAfterOrdinaryMovement(bool drag)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            Assert.False(game.ExecuteAction(Link(drag)));
            Assert.Empty(View(game).CanFollowUnitIds);
            Assert.Empty(View(game).CanDragUnitIds);
        }

        [Fact]
        public static void Follow_CopiesEveryStepWithoutUsingFollowersMovementAndExpiresAtTargetTurnEnd()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(2, Shadow(game).Unit.TargetUnitId);
            Assert.True(game.ExecuteAction(GeneralMove.New(2, At(game, 4, 6))));
            Assert.Equal(At(game, 3, 5), Shadow(game).Unit.Position);
            Assert.Equal(2, game.Events.OfType<ShadowAttachmentEvent>().Count());
            Assert.All(game.Events.OfType<ShadowAttachmentEvent>(), result => Assert.False(result.Blocked));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Null(Shadow(game).Unit.TargetUnitId);
            Assert.Equal(ShadowLinkMode.None, View(game).LinkMode);
            Assert.False(View(game).HasSelectedLinkThisTurn);
            Assert.True(Shadow(game).Unit.IsFirstAction);
            Assert.Contains(View(game).CanMoveGrids, entry => entry.distance == 2);
        }

        [Fact]
        public static void Follow_ExpiresWhenTargetPlayerUsesAnotherRole()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(3)));
            Assert.Null(Shadow(game).Unit.TargetUnitId);
        }

        [Fact]
        public static void Follow_SkipsOnlyBlockedStepAndThenTriesTheNextDirection()
        {
            Game game = MakeGame();
            game.Board.SetTerrain(At(game, 4, 3), Terrain.Wall);
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralMove.New(2, At(game, 5, 5))));
            Assert.Equal(At(game, 3, 4), Shadow(game).Unit.Position);
            Assert.Equal(new[] { true, false }, game.Events.OfType<ShadowAttachmentEvent>().Select(e => e.Blocked));
            Assert.Single(game.Events.OfType<ShadowCopyMoveEvent>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Copy_WrapsAtTheRecipientsBoundaryEvenForAnOrdinaryUnit(bool drag)
        {
            Game game = MakeGame(sourceRow: drag ? 3 : 1, sourceCol: drag ? 3 : 1,
                targetRow: drag ? 1 : 4, targetCol: drag ? 1 : 4);
            Assert.True(game.ExecuteAction(Link(drag)));
            if (!drag)
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralMove.New(drag ? 0 : 2, drag ? At(game, 3, 2) : At(game, 4, 3))));
            Assert.Equal(At(game, 1, 7), game.GetUnit(drag ? 2 : 0)!.Position);
        }

        [Theory]
        [InlineData("terrain-wall")]
        [InlineData("lake")]
        [InlineData("enemy-fort")]
        [InlineData("allied-wall")]
        [InlineData("line-wall")]
        public static void Drag_ChecksRecipientsTerrainBuildingsAndInnerWalls(string obstacle)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 4, 5);
            switch (obstacle)
            {
                case "terrain-wall": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "lake": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "enemy-fort": game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(MapConsts.FortHealth)!.SetPos(target)!); break;
                case "allied-wall": game.AddBuilding(WallBuilder.New().SetOwner(1)!.SetHealth(MapConsts.WallHealth)!.SetPos(target)!); break;
                case "line-wall":
                    int index = game.Board.VWallAt(At(game, 4, 4))!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(index / 64, 1ul << (index % 64), out _));
                    break;
            }
            Assert.True(game.ExecuteAction(ShadowDrag.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            Assert.Equal(At(game, 4, 4), game.GetUnit(2)!.Position);
            Assert.True(Assert.Single(game.Events.OfType<ShadowAttachmentEvent>()).Blocked);
            Assert.Empty(game.Events.OfType<ShadowCopyMoveEvent>());
            Assert.Equal(At(game, 3, 4), Shadow(game).Unit.Position);
        }

        [Fact]
        public static void Drag_CanEnterTargetsAlliedFortAndPreservesTargetsActionBudget()
        {
            Game game = MakeGame();
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(MapConsts.FortHealth)!.SetPos(At(game, 4, 5))!);
            Assert.True(game.ExecuteAction(ShadowDrag.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 5))));
            Assert.Equal(At(game, 4, 6), game.GetUnit(2)!.Position);
            Assert.Null(Shadow(game).Unit.TargetUnitId);
            Assert.Equal(ShadowLinkMode.None, View(game).LinkMode);
            Assert.True(game.GetUnit(2)!.IsFirstAction);
            Assert.Contains(game.GetUnit(2)!.GetView(game).CanMoveGrids, entry => entry.distance == 3);
            Assert.Equal(MapConsts.FortHealth, game.Buildings[0].Health);
            Assert.Equal(2, game.Events.OfType<ShadowAttachmentEvent>().Count());
            Assert.All(game.Events.OfType<ShadowAttachmentEvent>(), e => Assert.False(e.Blocked));
            Assert.DoesNotContain(typeof(ShadowAttachmentEvent).GetProperties(), p => p.PropertyType == typeof(Board.Node)
                || p.PropertyType == typeof(Board.Node[]));
        }

        [Theory]
        [InlineData("ink")]
        [InlineData("heavy")]
        [InlineData("alpha")]
        public static void Follow_ObservesDashChargeAndAttackRush(string target)
        {
            Game game = MakeGame(target);
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            int steps;
            if (target == "ink")
            {
                Assert.True(game.ExecuteAction(InkDash.New(2, At(game, 4, 6))));
                steps = 2;
            }
            else if (target == "heavy")
            {
                Assert.True(game.ExecuteAction(HeavyChargeStart.New(2, At(game, 4, 5))));
                Assert.True(game.ExecuteAction(HeavyChargeContinue.New(2, At(game, 4, 7))));
                steps = 3;
            }
            else
            {
                Assert.True(game.ExecuteAction(AlphaAttackRush.New(2, At(game, 4, 5))));
                steps = 1;
            }
            Assert.Equal(At(game, 3, 3 + steps), Shadow(game).Unit.Position);
            Assert.Equal(steps, game.Events.OfType<ShadowAttachmentEvent>().Count());
        }

        [Fact]
        public static void Follow_SurvivesRoundBoundary_ButDoesNotCopyWhirlpoolPull()
        {
            Game game = MakeGame(first: 1, threePlayers: true);
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(4)));
            game.Board.SetTerrain(At(game, 4, 5), Terrain.Whirlpool);
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(At(game, 4, 5), game.GetUnit(2)!.Position);
            // 漩涡吸附不算目标移动,附骨不复制这次位移。
            Assert.Equal(At(game, 3, 3), Shadow(game).Unit.Position);
            Assert.Equal(2, Shadow(game).Unit.TargetUnitId);
            Assert.Empty(game.Events.OfType<ShadowAttachmentEvent>());
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Null(Shadow(game).Unit.TargetUnitId);
        }

        [Fact]
        public static void MutualFollow_DoesNotRecurse()
        {
            Game game = MakeGame("shadow");
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(ShadowFollow.New(2, 0)));
            Assert.True(game.ExecuteAction(GeneralMove.New(2, At(game, 4, 5))));
            Assert.Equal(At(game, 3, 4), Shadow(game).Unit.Position);
            Assert.Equal(At(game, 4, 5), game.GetUnit(2)!.Position);
            Assert.Single(game.Events.OfType<ShadowAttachmentEvent>());
        }

        [Fact]
        public static void FollowAndDragOfSameRecipient_CopyOnlyOnceAndReportBothLinks()
        {
            Game game = MakeGame("shadow");
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(ShadowDrag.New(2, 0)));
            Assert.True(game.ExecuteAction(GeneralMove.New(2, At(game, 4, 5))));
            Assert.Equal(At(game, 3, 4), Shadow(game).Unit.Position);
            Assert.Single(game.Events.OfType<ShadowCopyMoveEvent>());
            Assert.Equal(2, game.Events.OfType<ShadowAttachmentEvent>().Count());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void HumanHiddenWolf_CannotBeSelected(bool drag)
        {
            Game game = MakeGame("hidden");
            Assert.False(game.GetUnit(2)!.CanBeAttached);
            Assert.DoesNotContain(2, View(game).CanFollowUnitIds);
            Assert.False(game.ExecuteAction(Link(drag)));
            Assert.False(Shadow(game).HasStartedTurn);
        }

        [Fact]
        public static void ExistingFollow_PausesWhenHiddenWolfBecomesHumanAndStillExpiresOnSchedule()
        {
            Game game = MakeGame("hidden", first: 1);
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(2)));
            Assert.False(game.GetUnit(2)!.CanBeAttached);
            Assert.Equal(2, Shadow(game).Unit.TargetUnitId);
            Assert.True(game.ExecuteAction(GeneralMove.New(2, At(game, 4, 5))));
            Assert.Equal(At(game, 3, 3), Shadow(game).Unit.Position);
            Assert.Empty(game.Events.OfType<ShadowAttachmentEvent>());
            Assert.Null(Shadow(game).Unit.TargetUnitId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Death_ClearsLinksToOrFromTheKilledUnit(bool killShadow)
        {
            Game game = MakeGame("sniper", targetRow: 3, targetCol: 4);
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            if (killShadow)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(SniperExecute.New(2, Shadow(game).Unit.Position)));
                Assert.False(Shadow(game).Unit.Alive);
            }
            else
            {
                Assert.True(game.ExecuteAction(ShadowAttack.New(0, At(game, 3, 4))));
                Assert.False(game.GetUnit(2)!.Alive);
                Assert.DoesNotContain(2, View(game).CanFollowUnitIds);
            }
            Assert.Null(Shadow(game).Unit.TargetUnitId);
            Assert.Equal(ShadowLinkMode.None, Shadow(game).Unit.LinkMode);
        }

        [Fact]
        public static void CloneAndView_PreserveLinkStateAndOwnAllTargetArrays()
        {
            Game game = MakeGame();
            ShadowUnitView view = View(game);
            ShadowUnitView copy = Assert.IsType<ShadowUnitView>(view.Clone());
            copy.CanFollowUnitIds[0] = 999;
            copy.CanDragUnitIds[0] = 999;
            Assert.DoesNotContain(999, view.CanFollowUnitIds);
            Assert.DoesNotContain(999, view.CanDragUnitIds);
            Assert.True(game.ExecuteAction(ShadowFollow.New(0, 2)));
            Shadow clone = Assert.IsType<Shadow>(Shadow(game).Clone());
            ShadowUnitView snapshot = View(game);
            Assert.True(clone.HasStartedTurn);
            Assert.True(clone.Unit.IsFirstAction);
            Assert.Equal(2, clone.Unit.TargetUnitId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Null(Shadow(game).Unit.TargetUnitId);
            Assert.Equal(2, clone.Unit.TargetUnitId);
            Assert.Equal(2, snapshot.TargetUnitId);
            Assert.True(snapshot.HasSelectedLinkThisTurn);
        }
    }
}
