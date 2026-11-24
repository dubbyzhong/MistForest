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
    public class PrinceFlowTests
    {
        private static Game MakeGame(params Func<Board, RoleBuilder>[] opponents)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder> { PrinceAt(board, 3, 3) })!,
            };
            foreach (Func<Board, RoleBuilder> opponent in opponents)
            {
                players.Add(New(new List<RoleBuilder> { opponent(board) })!);
            }

            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return Assert.IsType<Game>(builder.Build());
        }

        private static PrinceBuilder PrinceAt(Board board, int row, int col)
            => PrinceBuilder.New(new UnitBuilder[] { new PrinceUnitBuilder() }, board.At(row, col)!.Value)!;

        private static WolfBuilder WolfAt(Board board, int row, int col)
            => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(row, col)!.Value)!;

        private static Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static Prince Role(Game game) => Assert.IsType<Prince>(game.Roles[0]);
        private static PrinceUnitView View(Game game)
            => Assert.IsType<PrinceUnitView>(Role(game).Unit.GetView(game));

        [Fact]
        public static void Plant_UsesTheMoveAndDamagesEnemiesOnTheChosenTile()
        {
            Game game = MakeGame(board => WolfAt(board, 3, 3));
            Node origin = At(game, 3, 3);
            Assert.Contains(origin, View(game).CanPlantGrids);
            Assert.Contains(At(game, 3, 5), View(game).CanPlantGrids);
            Assert.DoesNotContain(At(game, 3, 6), View(game).CanPlantGrids);
            Assert.False(game.ExecuteAction(PrincePlantRose.New(0, At(game, 3, 6))));

            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, origin)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(UnitsPerPoint, game.GetUnit(1)!.Health);
            Assert.Equal(origin, Assert.Single(game.GetRoses()).Position);
            Assert.Equal(3 * HalfPoint, Assert.Single(View(game).Roses).health);
            Assert.Equal(1, Assert.Single(Assert.IsType<PrincePlantRoseEvent>(Assert.Single(game.Events)).UnitIds));
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanPlantGrids);
            Assert.False(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
        }

        [Fact]
        public static void Bloom_CanLandOnTheRoseAttackThatTileAndUseTwoRosesInOneTurn()
        {
            Game game = MakeGame(board => WolfAt(board, 7, 7));
            Node first = At(game, 3, 3);
            Node second = At(game, 3, 5);

            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, first)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, second)));

            var choices = View(game).CanBloomGrids;
            Assert.Contains(second, Assert.Single(choices, entry => entry.rose == second && entry.landing == second).attackGrids);
            Assert.False(game.ExecuteAction(PrinceBloom.New(0, second, first, first)));
            Assert.False(game.ExecuteAction(PrinceBloom.New(0, second, second, first)));
            Assert.True(game.ExecuteAction(PrinceBloom.New(0, second, second, second)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(second, Role(game).Unit.Position);
            Assert.DoesNotContain(View(game).CanBloomGrids, entry => entry.rose == second);
            Assert.False(game.ExecuteAction(PrinceBloom.New(0, second, second, second)));
            Assert.True(game.ExecuteAction(PrinceBloom.New(0, first, first, first)));

            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(first, Role(game).Unit.Position);
            Assert.All(game.GetRoses(), rose => Assert.Equal(UnitsPerPoint, rose.Health));
            Assert.Equal(2, game.GetEventsForPlayer(1).OfType<PrinceBloomEvent>().Count());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<PrinceBlinkEvent>());
            Assert.Equal(2, game.GetEventsForPlayer(0).OfType<PrinceBlinkEvent>().Count());
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Contains(View(game).CanBloomGrids, entry => entry.rose == second);
        }

        [Fact]
        public static void Bloom_KeepsTheOrdinaryMoveAndSilenceOnlyBlocksTheSkills()
        {
            Game game = MakeGame(board => WolfAt(board, 7, 7));
            Node rose = At(game, 3, 4);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, rose)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(PrinceBloom.New(0, rose, rose, rose)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 5))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));

            var silence = typeof(Unit).GetMethod("TrySilence",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.True((bool)silence.Invoke(Role(game).Unit, null)!);
            Assert.Empty(View(game).CanPlantGrids);
            Assert.Empty(View(game).CanBloomGrids);
            Assert.False(game.ExecuteAction(PrincePlantRose.New(0, At(game, 3, 5))));
            Assert.False(game.ExecuteAction(PrinceBloom.New(0, rose, rose, rose)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, rose)));
        }

        [Fact]
        public static void Rose_TakesIndependentDamageThroughAFortAndResetsNextRound()
        {
            Game game = MakeGame(board => InkBuilder.New(
                new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 5)!.Value)!);
            Node target = At(game, 3, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2 * UnitsPerPoint)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
            Assert.Equal(3 * HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.True(game.ExecuteAction(PrinceBloom.New(0, target, target, At(game, 3, 3))));
            Assert.Equal(1, game.CurrentPlayerId);

            PrinceRose rose = Assert.Single(game.GetRoses());
            Assert.Equal(UnitsPerPoint, rose.Health);
            Assert.True(game.ExecuteAction(InkAttack.New(1, target)));
            Assert.Equal(HalfPoint, rose.Health);
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(UnitsPerPoint, game.Buildings[0].Health);
            Assert.True(game.ExecuteAction(InkAttack.New(1, target)));
            Assert.Equal(HalfPoint, rose.Health);
            Assert.Equal(HalfPoint, rose.HighestDirectDamage);
            Assert.Single(game.Events.OfType<PrinceRoseDamageEvent>());

            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, rose.HighestDirectDamage);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(InkAttack.New(1, target)));
            Assert.Empty(game.GetRoses());
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(3 * HalfPoint, Role(game).Unit.Health);
            Assert.Equal(0, game.Events.OfType<PrinceRoseDamageEvent>().Last().RemainingHealth);
        }

        [Fact]
        public static void Wolf_CanBiteAnEnemyRoseInAFortWithoutDamagingTheFort()
        {
            Game game = MakeGame(board => WolfAt(board, 3, 5));
            Node target = At(game, 3, 4);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2 * UnitsPerPoint)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            Assert.True(game.ExecuteAction(WolfBite.New(1, target)));
            Assert.Equal(HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.Equal(3 * HalfPoint, game.Buildings[0].Health);
            WolfBiteEvent bite = Assert.Single(game.Events.OfType<WolfBiteEvent>());
            Assert.Null(bite.BuildingId);
            Assert.Empty(bite.UnitIds);
        }

        [Theory]
        [InlineData(nameof(Alpha), false)]
        [InlineData(nameof(Alpha), true)]
        [InlineData(nameof(HiddenWolf), false)]
        [InlineData(nameof(HiddenWolf), true)]
        [InlineData(nameof(Knight), false)]
        [InlineData(nameof(Knight), true)]
        [InlineData(nameof(Shadow), false)]
        [InlineData(nameof(Shadow), true)]
        public static void SingleGridAttack_DamagesTheRoseEvenWhenAFortProtectsTheUnit(string attacker, bool withFort)
        {
            Game game = MakeGame(board =>
            {
                Node start = board.At(3, 4)!.Value;
                return attacker switch
                {
                    nameof(Alpha) => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, start)!,
                    nameof(HiddenWolf) => HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, start)!,
                    nameof(Knight) => KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, start)!,
                    nameof(Shadow) => ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() }, start)!,
                    _ => throw new ArgumentOutOfRangeException(nameof(attacker)),
                };
            });
            Node target = Role(game).Unit.Position;
            if (withFort)
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(UnitsPerPoint)!.SetPos(target)!);
            }
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            if (attacker == nameof(Knight))
            {
                Assert.True(game.ExecuteAction(KnightHunt.New(1, At(game, 3, 4))));
            }
            GameAction action = attacker switch
            {
                nameof(Alpha) => AlphaAttack.New(1, target),
                nameof(HiddenWolf) => HiddenWolfAttack.New(1, target),
                nameof(Knight) => KnightCleave.New(1, target),
                nameof(Shadow) => ShadowAttack.New(1, target),
                _ => throw new ArgumentOutOfRangeException(nameof(attacker)),
            };
            Assert.True(game.ExecuteAction(action));

            Assert.Equal(HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.Equal(target, Assert.Single(game.Events.OfType<PrinceRoseDamageEvent>()).Target);
            Assert.Equal(withFort ? 3 * HalfPoint : HalfPoint, Role(game).Unit.Health);
            if (withFort)
            {
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Sword_CanDamageAPlacedRoseWithEitherAttack(bool flyingSword)
        {
            Game game = MakeGame(board => SwordBuilder.New(
                new UnitBuilder[] { new SwordUnitBuilder() }, board.At(3, 5)!.Value)!);
            Node target = At(game, 3, 4);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            Assert.True(game.ExecuteAction(flyingSword ? SwordThrow.New(1, target) : SwordStab.New(1, target)));
            Assert.Equal(HalfPoint, Assert.Single(game.GetRoses()).Health);
            Assert.Equal(target, Assert.Single(game.Events.OfType<PrinceRoseDamageEvent>()).Target);
        }

        [Fact]
        public static void Roses_CapAtThreeAndClonedRosesSurviveLiveBloomChanges()
        {
            Game game = MakeGame(board => WolfAt(board, 7, 7));
            Node first = At(game, 3, 3);
            foreach (Node target in new[] { first, At(game, 3, 4), At(game, 4, 3) })
            {
                Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            }
            Assert.Equal(3, game.GetRoses().Length);
            Node fourth = At(game, 4, 4);
            Assert.Empty(View(game).CanPlantGrids);
            Assert.False(game.ExecuteAction(PrincePlantRose.New(0, fourth)));

            Prince clone = Assert.IsType<Prince>(Role(game).Clone());
            PrinceUnitView view = View(game);
            PrinceUnitView viewClone = Assert.IsType<PrinceUnitView>(view.Clone());
            Assert.NotSame(view.CanBloomGrids[0].attackGrids, viewClone.CanBloomGrids[0].attackGrids);
            Assert.NotSame(view.Roses, viewClone.Roses);
            for (int i = 0; i < 3; ++i)
            {
                Assert.True(game.ExecuteAction(PrinceBloom.New(0, first, first, first)));
                if (i < 2)
                {
                    Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                    Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
                }
            }
            Assert.Equal(2, game.GetRoses().Length);
            Assert.Equal(3 * HalfPoint, Assert.IsType<PrinceUnitView>(clone.Unit.GetView(game))
                .Roses.Single(entry => entry.position == first).health);
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, fourth)));
            Assert.Equal(3, game.GetRoses().Length);
        }

        [Fact]
        public static void Bloom_ShadowCopiesTeleportAndOnlyTheOwnersReceiveThePaths()
        {
            Game game = MakeGame(
                board => ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() }, board.At(6, 6)!.Value)!,
                board => WolfAt(board, 7, 7));
            Node target = At(game, 3, 5);
            Assert.True(game.Board.SetTerrain(At(game, 6, 7), Terrain.Wall));
            Assert.True(game.ExecuteAction(PrincePlantRose.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(ShadowFollow.New(1, 0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(PrinceBloom.New(0, target, target, target)));

            Assert.Equal(target, Role(game).Unit.Position);
            Assert.Equal(At(game, 6, 1), game.GetUnit(1)!.Position);
            Assert.Single(game.GetEventsForPlayer(0).OfType<PrinceBlinkEvent>());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<PrinceBlinkEvent>());
            Assert.Empty(game.GetEventsForPlayer(2).OfType<PrinceBlinkEvent>());
            Assert.Empty(game.GetEventsForPlayer(0).OfType<ShadowCopyMoveEvent>());
            Assert.Single(game.GetEventsForPlayer(1).OfType<ShadowCopyMoveEvent>());
            Assert.Empty(game.GetEventsForPlayer(2).OfType<ShadowCopyMoveEvent>());
        }
    }
}
