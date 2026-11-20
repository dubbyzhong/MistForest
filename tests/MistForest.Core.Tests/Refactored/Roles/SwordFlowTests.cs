using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.Consts.CombatConsts;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SwordFlowTests
    {
        private static Game MakeGame(params (int row, int col)[] opponents)
            => MakeGameWithShadow(null, opponents);

        private static Game MakeGameWithShadow(
            (int row, int col)? shadowPosition, params (int row, int col)[] opponents)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder>
                {
                    SwordBuilder.New(new UnitBuilder[] { new SwordUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
            };
            if (shadowPosition is { } shadow)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() },
                        board.At(shadow.row, shadow.col)!.Value)!,
                })!);
            }
            foreach ((int row, int col) in opponents)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(row, col)!.Value)!,
                })!);
            }

            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            if (shadowPosition is not null)
            {
                Assert.NotNull(builder.SetFirstPlayerId(1));
            }
            return Assert.IsType<Game>(builder.Build());
        }

        private static Sword Role(Game game) => Assert.IsType<Sword>(game.Roles[0]);
        private static Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static SwordUnitView View(Game game)
            => Assert.IsType<SwordUnitView>(Role(game).Unit.GetView(game));

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(1, 0)]
        [InlineData(0, -1)]
        [InlineData(0, 1)]
        public static void Stab_HitsOnlyTheChosenAdjacentCell(int dx, int dy)
        {
            Game game = MakeGame((3 + dx, 3 + dy), (3 + 2 * dx, 3 + 2 * dy));
            Node first = At(game, 3 + dx, 3 + dy);
            Node second = At(game, 3 + 2 * dx, 3 + 2 * dy);

            Assert.Equal(9, View(game).SwordQi);
            Assert.Contains(first, View(game).CanStabGrids);
            Assert.DoesNotContain(second, View(game).CanStabGrids);
            Assert.False(game.ExecuteAction(SwordStab.New(0, At(game, 4, 4))));
            Assert.False(game.ExecuteAction(SwordStab.New(0, second)));
            Assert.True(game.ExecuteAction(SwordStab.New(0, first)));

            SwordStabEvent attack = Assert.IsType<SwordStabEvent>(Assert.Single(game.Events));
            Assert.Equal(first, attack.Target);
            Assert.Equal(1, Assert.Single(attack.UnitIds));
            Assert.Equal(HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(3 * HalfPoint, game.GetUnit(2)!.Health);
            Assert.Equal(At(game, 3, 3), Role(game).Unit.Position);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Equal(new[] { Role(game).Unit.Position, first }, View(game).CanAdvanceGrids);
        }

        [Fact]
        public static void Move_UsesTheOrdinaryActionAndRejectsTheStab()
        {
            Game game = MakeGame((7, 7));
            Assert.False(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 5))));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            Assert.Equal(At(game, 3, 4), Role(game).Unit.Position);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanStabGrids);
            Assert.False(game.ExecuteAction(SwordStab.New(0, At(game, 3, 5))));
        }

        [Fact]
        public static void Stab_OwnGridHitsOnlyEnemiesOnThatGrid()
        {
            Game game = MakeGame((3, 3), (3, 4));
            Node origin = At(game, 3, 3);
            Assert.Contains(origin, View(game).CanStabGrids);
            Assert.True(game.ExecuteAction(SwordStab.New(0, origin)));

            SwordStabEvent attack = Assert.IsType<SwordStabEvent>(Assert.Single(game.Events));
            Assert.Equal(origin, attack.Target);
            Assert.Equal(1, Assert.Single(attack.UnitIds));
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(3 * HalfPoint, game.GetUnit(2)!.Health);
            Assert.Equal(origin, Assert.Single(View(game).CanAdvanceGrids));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void Stab_TerrainAndLineWallsBlockTheAdjacentTarget(bool lineWall)
        {
            Game game = MakeGame((7, 7));
            Node target = At(game, 3, 4);
            if (lineWall)
            {
                int edge = game.Board.VWallAt(At(game, 3, 3))!.Value;
                Assert.True(game.Board.VerticalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _));
            }
            else
            {
                Assert.True(game.Board.SetTerrain(target, Terrain.Wall));
            }

            Assert.DoesNotContain(target, View(game).CanStabGrids);
            Assert.False(game.ExecuteAction(SwordStab.New(0, target)));
            Assert.Empty(game.Events);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Stab_DestroyedBuildingProtectsItsOccupant()
        {
            Game game = MakeGame((3, 4), (3, 5));
            Node first = At(game, 3, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(first)!);
            Assert.True(game.ExecuteAction(SwordStab.New(0, first)));

            SwordStabEvent attack = Assert.IsType<SwordStabEvent>(Assert.Single(game.Events));
            Assert.Equal(first, attack.Target);
            Assert.Empty(attack.UnitIds);
            Assert.Equal(0, attack.BuildingId);
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(3 * HalfPoint, game.GetUnit(2)!.Health);
            Assert.Contains(first, View(game).CanAdvanceGrids);
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, first)));
            Assert.Equal(first, Role(game).Unit.Position);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Stab_SpendsOneQiAndOwnerTurnEndRecoversThreeUpToTheCap()
        {
            Game game = MakeGame((7, 7));
            Sword sword = Role(game);
            var qi = typeof(SwordUnit).GetProperty(nameof(SwordUnit.SwordQi))!;
            Node target = At(game, 3, 4);

            qi.SetValue(sword.Unit, 0);
            Assert.Empty(View(game).CanStabGrids);
            Assert.False(game.ExecuteAction(SwordStab.New(0, target)));
            qi.SetValue(sword.Unit, 2);
            Assert.True(game.ExecuteAction(SwordStab.New(0, target)));
            Assert.Equal(1, sword.Unit.SwordQi);
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, sword.Unit.Position)));
            Assert.Equal(4, sword.Unit.SwordQi);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(4, sword.Unit.SwordQi);
            qi.SetValue(sword.Unit, 8);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(9, sword.Unit.SwordQi);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Advance_RequiresTheFollowUpChoiceAndReportsTheResultPrivately(bool enter)
        {
            Game game = MakeGame((7, 7));
            Node origin = Role(game).Unit.Position;
            Node target = At(game, 3, 4);
            Node unrelated = At(game, 4, 3);
            Assert.False(game.ExecuteAction(SwordAdvance.New(0, origin)));
            Assert.True(game.ExecuteAction(SwordStab.New(0, target)));

            SwordStabEvent attack = Assert.IsType<SwordStabEvent>(Assert.Single(game.GetEventsForPlayer(1)));
            Assert.Equal(target, attack.Target);
            Assert.Equal(new[] { origin, target }, View(game).CanAdvanceGrids);
            Assert.Empty(View(game).CanStabGrids);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanBuildFortGrids);
            Assert.False(game.ExecuteAction(SwordAdvance.New(0, unrelated)));
            Assert.False(game.ExecuteAction(GeneralMove.New(0, target)));
            Assert.False(game.ExecuteAction(SwordStab.New(0, target)));
            Assert.False(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(0, game.CurrentPlayerId);

            Sword clone = Assert.IsType<Sword>(Role(game).Clone());
            SwordUnitView view = View(game);
            SwordUnitView viewClone = Assert.IsType<SwordUnitView>(view.Clone());
            Assert.NotSame(view.CanAdvanceGrids, viewClone.CanAdvanceGrids);
            Node destination = enter ? target : origin;
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, destination)));
            Assert.Equal(destination, Role(game).Unit.Position);
            Assert.Equal(enter, game.Board.GetGrid(target)!.Units.Contains(0));
            Assert.Equal(!enter, game.Board.GetGrid(origin)!.Units.Contains(0));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanAdvanceGrids);
            Assert.Equal(new[] { origin, target }, Assert.IsType<SwordUnitView>(clone.Unit.GetView(game)).CanAdvanceGrids);

            SwordAdvanceEvent advance = Assert.Single(game.GetEventsForPlayer(0).OfType<SwordAdvanceEvent>());
            Assert.Equal(origin, advance.From);
            Assert.Equal(destination, advance.Target);
            Assert.Same(attack, Assert.Single(game.GetEventsForPlayer(1)));
        }

        [Fact]
        public static void Advance_SurvivingEnemyFortAllowsOnlyStaying()
        {
            Game game = MakeGame((7, 7));
            Node origin = Role(game).Unit.Position;
            Node target = At(game, 3, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2 * UnitsPerPoint)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(SwordStab.New(0, target)));

            Assert.Equal(UnitsPerPoint, game.Buildings[0].Health);
            Assert.Equal(origin, Assert.Single(View(game).CanAdvanceGrids));
            Assert.False(game.ExecuteAction(SwordAdvance.New(0, target)));
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, origin)));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Advance_RejectsATargetThatBecameBlockedAfterTheViewWasRead()
        {
            Game game = MakeGame((7, 7));
            Node origin = Role(game).Unit.Position;
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(SwordStab.New(0, target)));
            Assert.Contains(target, View(game).CanAdvanceGrids);
            Assert.True(game.Board.SetTerrain(target, Terrain.Wall));

            Assert.False(game.ExecuteAction(SwordAdvance.New(0, target)));
            Assert.Equal(origin, Assert.Single(View(game).CanAdvanceGrids));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, origin)));
        }

        [Fact]
        public static void Advance_ShadowCopiesTheStepWithoutPublicMovementFeedback()
        {
            Game game = MakeGameWithShadow((6, 6), (7, 7));
            Unit shadow = game.GetUnit(1)!;
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(ShadowFollow.New(1, 0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(SwordStab.New(0, target)));
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, target)));

            Assert.Equal(target, Role(game).Unit.Position);
            Assert.Equal(At(game, 6, 7), shadow.Position);
            Assert.Single(game.GetEventsForPlayer(0).OfType<SwordAdvanceEvent>());
            Assert.Empty(game.GetEventsForPlayer(0).OfType<ShadowCopyMoveEvent>());
            Assert.Single(game.GetEventsForPlayer(1).OfType<ShadowCopyMoveEvent>());
            Assert.Single(game.GetEventsForPlayer(1).OfType<ShadowAttachmentEvent>());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<SwordAdvanceEvent>());
            Assert.Empty(game.GetEventsForPlayer(2).OfType<SwordAdvanceEvent>());
            Assert.Empty(game.GetEventsForPlayer(2).OfType<ShadowCopyMoveEvent>());
            Assert.Empty(game.GetEventsForPlayer(2).OfType<ShadowAttachmentEvent>());
        }

        [Fact]
        public static void Throw_ViewAndRequestRequireAnAffordableStraightTarget()
        {
            Game game = MakeGame((3, 4), (3, 6));
            Node target = At(game, 3, 6);
            SwordUnit unit = Role(game).Unit;
            var qi = typeof(SwordUnit).GetProperty(nameof(SwordUnit.SwordQi))!;

            qi.SetValue(unit, 2);
            Assert.Contains((unit.Position, 1), View(game).CanThrowGrids);
            Assert.DoesNotContain(View(game).CanThrowGrids, entry => entry.node == target);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, target)));
            qi.SetValue(unit, 4);
            Assert.Contains((target, 3), View(game).CanThrowGrids);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, At(game, 4, 4))));
            Assert.False(game.ExecuteAction(SwordThrow.New(0, At(game, 3, 7))));
            Assert.Empty(game.Events);
            Assert.True(game.ExecuteAction(SwordThrow.New(0, target)));

            SwordThrowEvent attack = Assert.IsType<SwordThrowEvent>(Assert.Single(game.GetEventsForPlayer(1)));
            Assert.Equal(target, attack.Target);
            Assert.Equal(2, Assert.Single(attack.UnitIds));
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            Assert.Equal(HalfPoint, game.GetUnit(2)!.Health);
            Assert.Equal(At(game, 3, 3), unit.Position);
            Assert.Equal(target, Assert.Single(View(game).PlacedSwordGrids));
            Assert.Equal(8, View(game).SwordQiCap);
            Assert.Equal(4, View(game).SwordQi);
            Assert.Empty(View(game).CanAdvanceGrids);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData("terrain")]
        [InlineData("line wall")]
        [InlineData("fort")]
        public static void Throw_BlockersStopTheRayAndAHitFortProtectsItsOccupant(string blocker)
        {
            Game game = MakeGame((3, 4), (7, 7));
            Node first = At(game, 3, 4);
            Node beyond = At(game, 3, 5);
            if (blocker == "terrain")
            {
                Assert.True(game.Board.SetTerrain(first, Terrain.Wall));
            }
            else if (blocker == "line wall")
            {
                int edge = game.Board.VWallAt(Role(game).Unit.Position)!.Value;
                Assert.True(game.Board.VerticalWalls.SetInner(edge / 64, 1ul << (edge % 64), out _));
            }
            else
            {
                game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(first)!);
            }

            Assert.DoesNotContain(View(game).CanThrowGrids, entry => entry.node == beyond);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, beyond)));
            Assert.Equal(blocker == "fort", game.ExecuteAction(SwordThrow.New(0, first)));
            Assert.Equal(3 * HalfPoint, game.GetUnit(1)!.Health);
            if (blocker == "fort")
            {
                SwordThrowEvent attack = Assert.IsType<SwordThrowEvent>(Assert.Single(game.Events));
                Assert.Equal(0, attack.BuildingId);
                Assert.Empty(attack.UnitIds);
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
                Assert.Equal(first, Assert.Single(View(game).PlacedSwordGrids));
            }
        }

        [Fact]
        public static void Throw_LeaveInPlaceLowersTheCapAndClonesKeepIndependentSwords()
        {
            Game game = MakeGame((7, 7));
            SwordUnit unit = Role(game).Unit;
            Node origin = unit.Position;
            Assert.True(game.ExecuteAction(SwordThrow.New(0, origin)));
            Assert.Equal(8, View(game).SwordQi);

            Sword clone = Assert.IsType<Sword>(Role(game).Clone());
            SwordUnitView view = View(game);
            SwordUnitView clonedView = Assert.IsType<SwordUnitView>(view.Clone());
            view.PlacedSwordGrids[0] = At(game, 7, 7);
            Assert.Equal(origin, Assert.Single(clonedView.PlacedSwordGrids));

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            var qi = typeof(SwordUnit).GetProperty(nameof(SwordUnit.SwordQi))!;
            qi.SetValue(unit, 0);
            Assert.Empty(View(game).CanThrowGrids);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, origin)));
            qi.SetValue(unit, 1);
            Assert.True(game.ExecuteAction(SwordThrow.New(0, origin)));
            Assert.Equal(new[] { origin, origin }, View(game).PlacedSwordGrids);
            Assert.Equal(7, View(game).SwordQiCap);
            Assert.Equal(3, View(game).SwordQi);
            Assert.Equal(origin, Assert.Single(Assert.IsType<SwordUnitView>(clone.Unit.GetView(game)).PlacedSwordGrids));
            Assert.Equal(8, clone.Unit.SwordQiCap);
        }

        [Fact]
        public static void SwordSkills_CannotStartWhileSilencedOrWhileAwaitingTheStabFollowUp()
        {
            Game game = MakeGame((7, 7));
            Node origin = Role(game).Unit.Position;
            Assert.True(game.ExecuteAction(SwordThrow.New(0, origin)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));

            var silence = typeof(Unit).GetMethod("TrySilence",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.True((bool)silence.Invoke(Role(game).Unit, null)!);
            Assert.Empty(View(game).CanThrowGrids);
            Assert.Empty(View(game).CanRecallGrids);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, origin)));
            Assert.False(game.ExecuteAction(SwordRecall.New(0, origin)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));

            Assert.Contains((origin, 1), View(game).CanThrowGrids);
            Assert.Equal((origin, 0), Assert.Single(View(game).CanRecallGrids));
            Assert.True(game.ExecuteAction(SwordStab.New(0, origin)));
            Assert.Empty(View(game).CanThrowGrids);
            Assert.Empty(View(game).CanRecallGrids);
            Assert.False(game.ExecuteAction(SwordThrow.New(0, origin)));
            Assert.False(game.ExecuteAction(SwordRecall.New(0, origin)));
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, origin)));
        }

        [Fact]
        public static void Recall_PaysTheCurrentDistanceAndDoesNotNeedAClearShot()
        {
            Game game = MakeGame((7, 7));
            SwordUnit unit = Role(game).Unit;
            Node target = At(game, 3, 6);
            Assert.True(game.ExecuteAction(SwordThrow.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.Board.SetTerrain(At(game, 3, 5), Terrain.Wall));

            var qi = typeof(SwordUnit).GetProperty(nameof(SwordUnit.SwordQi))!;
            qi.SetValue(unit, 2);
            Assert.Empty(View(game).CanRecallGrids);
            Assert.False(game.ExecuteAction(SwordRecall.New(0, target)));
            qi.SetValue(unit, 4);
            Assert.Equal((target, 3), Assert.Single(View(game).CanRecallGrids));
            Assert.False(game.ExecuteAction(SwordRecall.New(0, At(game, 3, 4))));
            Assert.True(game.ExecuteAction(SwordRecall.New(0, target)));

            SwordRecallEvent recall = Assert.Single(game.GetEventsForPlayer(1).OfType<SwordRecallEvent>());
            Assert.Equal(0, recall.UnitId);
            Assert.Equal(target, recall.Target);
            Assert.Equal(1, View(game).SwordQi);
            Assert.Equal(9, View(game).SwordQiCap);
            Assert.Equal(1, View(game).ExtraStabs);
            Assert.Empty(View(game).PlacedSwordGrids);
            Assert.Empty(View(game).CanThrowGrids);
            Assert.False(game.ExecuteAction(SwordRecall.New(0, target)));
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Recall_StackedSwordsGrantSeparateStabsAfterTheOrdinaryStab()
        {
            Game game = MakeGame((7, 7));
            Node origin = Role(game).Unit.Position;
            Node next = At(game, 3, 4);
            for (int i = 0; i < 2; ++i)
            {
                Assert.True(game.ExecuteAction(SwordThrow.New(0, origin)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            }

            Assert.True(game.ExecuteAction(SwordStab.New(0, next)));
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, next)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanStabGrids);
            SwordUnitView view = View(game);
            SwordUnitView viewClone = Assert.IsType<SwordUnitView>(view.Clone());
            Assert.NotSame(view.CanRecallGrids, viewClone.CanRecallGrids);
            Assert.Equal((origin, 1), Assert.Single(view.CanRecallGrids));
            Assert.True(game.ExecuteAction(SwordRecall.New(0, origin)));
            Assert.Equal(origin, Assert.Single(View(game).PlacedSwordGrids));
            Assert.True(game.ExecuteAction(SwordRecall.New(0, origin)));
            Assert.Equal(2, View(game).ExtraStabs);
            Assert.Equal(4, View(game).SwordQi);
            Sword clone = Assert.IsType<Sword>(Role(game).Clone());

            Assert.True(game.ExecuteAction(SwordStab.New(0, next)));
            Assert.Equal(1, View(game).ExtraStabs);
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, next)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(SwordStab.New(0, origin)));
            Assert.Equal(0, View(game).ExtraStabs);
            Assert.Equal(2, View(game).SwordQi);
            Assert.True(game.ExecuteAction(SwordAdvance.New(0, origin)));

            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(origin, Role(game).Unit.Position);
            Assert.Equal(2, clone.Unit.ExtraStabs);
            Assert.Equal(3, game.GetEventsForPlayer(0).OfType<SwordAdvanceEvent>().Count());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<SwordAdvanceEvent>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Recall_AfterOrdinaryMoveAllowsOnlyTheEarnedStab(bool useExtraStab)
        {
            Game game = MakeGame((7, 7));
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(SwordThrow.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, target)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.False(game.ExecuteAction(SwordStab.New(0, target)));

            Assert.Equal((target, 0), Assert.Single(View(game).CanRecallGrids));
            Assert.True(game.ExecuteAction(SwordRecall.New(0, target)));
            Assert.Equal(8, View(game).SwordQi);
            Assert.Equal(1, View(game).ExtraStabs);
            if (useExtraStab)
            {
                Assert.True(game.ExecuteAction(SwordStab.New(0, target)));
                Assert.Equal(0, View(game).ExtraStabs);
                Assert.Equal(7, View(game).SwordQi);
                Assert.True(game.ExecuteAction(SwordAdvance.New(0, target)));
            }
            else
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            }

            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, View(game).ExtraStabs);
        }

        [Theory]
        [InlineData("fort", typeof(GeneralBuildFortEvent))]
        [InlineData("wall", typeof(GeneralBuildWallEvent))]
        [InlineData("demolish", typeof(GeneralDemolishEvent))]
        [InlineData("surrender", typeof(GeneralSurrenderEvent))]
        public static void GeneralActions_UseTheNormalGameEntry(string kind, Type eventType)
        {
            Game game = MakeGame((7, 7));
            Node target = At(game, 3, 4);
            if (kind == "demolish")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(target)!);
            }
            GameAction action = kind switch
            {
                "fort" => GeneralBuildFort.New(0, target),
                "wall" => GeneralBuildWall.New(0, new[] { target })!,
                "demolish" => GeneralDemolish.New(0, target),
                _ => GeneralSurrender.New(0),
            };

            Assert.True(game.ExecuteAction(action));
            Assert.IsType(eventType, Assert.Single(game.Events));
        }
    }
}
