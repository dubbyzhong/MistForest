using System.Reflection;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class KnightHuntTests
    {
        internal static Game MakeGame(string enemy = "wolf")
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node position = board.At(4, 5)!.Value;
            RoleBuilder foe = enemy switch
            {
                "hidden" => HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, position)!,
                "alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, position)!,
                "ink" => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, position)!,
                "armiger" => ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, position)!,
                "heavy" => HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, position)!,
                "shadow" => ShadowBuilder.New(new UnitBuilder[] { new ShadowUnitBuilder() }, position)!,
                "knight" => KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, position)!,
                _ => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position)!,
            };
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board))!.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    KnightBuilder.New(new UnitBuilder[] { new KnightUnitBuilder() }, board.At(4, 4)!.Value)!,
                })!,
                New(new List<RoleBuilder> { foe })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            }));
            return builder.Build()!;
        }

        private static Knight Role(Game game) => Assert.IsType<Knight>(game.Roles[0]);
        private static KnightUnitView View(Game game) => Assert.IsType<KnightUnitView>(Role(game).Unit.GetView(game));
        private static Board.Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static void Hunt(Game game) => Assert.True(game.ExecuteAction(KnightHunt.New(0, Role(game).Unit.Position)));
        private static void NextRound(Game game)
        {
            if (game.CurrentPlayerId == 0) Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
        }

        internal static bool ApplySilence(Unit unit)
            => (bool)typeof(Unit).GetMethod("TrySilence", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(unit, null)!;

        [Fact]
        public static void HuntAndCleave_UseTheSamePerRoundDamageCap()
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.Board.GetGrid(enemy.Position)!.Units.Remove(1);
            enemy.Position = At(game, 6, 5);
            game.Board.GetGrid(enemy.Position)!.Units.Add(1);
            Assert.True(game.ExecuteAction(KnightHunt.New(0, enemy.Position)));
            Assert.Equal(2, enemy.Health);
            Assert.True(game.ExecuteAction(KnightCleave.New(0, enemy.Position)));
            Assert.Equal(2, enemy.Health);
            Assert.Equal(4, enemy.HighestDirectDamage);
        }

        [Fact]
        public static void Hunt_OffersCurrentGridAndLegalJumpsAndPublishesDamageAtLanding()
        {
            Game game = MakeGame();
            Assert.Equal(9, View(game).CanHuntGrids.Length);
            Unit foe = game.GetUnit(1)!;
            game.Board.GetGrid(foe.Position)!.Units.Remove(1);
            foe.Position = At(game, 6, 5);
            game.Board.GetGrid(foe.Position)!.Units.Add(1);
            Assert.True(game.ExecuteAction(KnightHunt.New(0, foe.Position)));
            Assert.Equal(foe.Position, Role(game).Unit.Position);
            Assert.Equal(2, foe.Health);
            Assert.True(View(game).IsHunting);
            Assert.True(View(game).CanSilence);
            Assert.Equal(0, Role(game).Unit.OptionsUsed);
            Assert.Equal(0, game.CurrentPlayerId);
            KnightAttackEvent attack = Assert.Single(game.Events.OfType<KnightAttackEvent>());
            Assert.True(attack.IsHunt);
            Assert.Equal(foe.Position, attack.Target);
            Assert.Equal(new[] { 1 }, attack.UnitIds);
            Assert.Contains(attack, game.GetEventsForPlayer(1));
            Assert.Empty(View(game).CanLeapGrids);
            Assert.Empty(View(game).CanHuntGrids);
            Assert.False(game.ExecuteAction(GeneralBuildFort.New(0, At(game, 5, 5))));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public static void FollowUps_EnforceIncreasingOrderAndAtMostTwoChoices(int first)
        {
            Game game = MakeGame();
            Hunt(game);
            Assert.True(game.ExecuteAction(first switch
            {
                1 => KnightSilence.New(0),
                2 => KnightCleave.New(0, At(game, 4, 3)),
                _ => (MistForest.Core.GameRefactored.Action)KnightSecretLeap.New(0, At(game, 6, 5)),
            }));
            Assert.Equal((KnightOption)first, Role(game).Unit.LastOption);
            Assert.Equal(1, Role(game).Unit.OptionsUsed);
            Assert.False(Role(game).JudgeAction(KnightSilence.New(0), game));
            if (first < 3)
            {
                Assert.Equal(0, game.CurrentPlayerId);
                Assert.True(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 6, 5))));
                Assert.Equal(2, Role(game).Unit.OptionsUsed);
            }
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Empty(View(game).CanSecretLeapGrids);
        }

        [Fact]
        public static void FollowUps_SilenceThenCleaveSkipsRetreatAndCooldownsBlockExactlyOneOwnTurn()
        {
            Game game = MakeGame();
            Hunt(game);
            Assert.True(game.ExecuteAction(KnightSilence.New(0)));
            Assert.True(game.ExecuteAction(KnightCleave.New(0, At(game, 4, 5))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 6, 5))));
            NextRound(game);
            Assert.Equal(1, View(game).SilenceCooldown);
            Assert.Equal(1, View(game).CleaveCooldown);
            Hunt(game);
            Assert.False(View(game).CanSilence);
            Assert.Empty(View(game).CanCleaveGrids);
            Assert.NotEmpty(View(game).CanSecretLeapGrids);
            Assert.True(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 6, 5))));
            Assert.Equal(1, Role(game).Unit.OptionsUsed);
            NextRound(game);
            Hunt(game);
            Assert.True(View(game).CanSilence);
            Assert.NotEmpty(View(game).CanCleaveGrids);
            Assert.Empty(View(game).CanSecretLeapGrids);
            Assert.Equal(1, View(game).SecretLeapCooldown);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Cleave_CanRushAfterDestroyingABuildingAndDoesNotOverflowIntoOccupants(bool rush)
        {
            Game game = MakeGame();
            Board.Node target = game.GetUnit(1)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(4)!.SetPos(target)!);
            Hunt(game);
            Assert.Contains(target, View(game).CanCleaveRushGrids);
            Assert.True(game.ExecuteAction(rush ? KnightCleaveRush.New(0, target) : KnightCleave.New(0, target)));
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(6, game.GetUnit(1)!.Health);
            Assert.Equal(rush ? target : At(game, 4, 4), Role(game).Unit.Position);
            KnightAttackEvent hit = game.Events.OfType<KnightAttackEvent>().Last();
            Assert.False(hit.IsHunt);
            Assert.Equal(0, hit.BuildingId);
            Assert.Empty(hit.UnitIds);
            Assert.Equal(1, Role(game).Unit.OptionsUsed);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData("strong-fort")]
        [InlineData("wall-terrain")]
        [InlineData("lake")]
        [InlineData("line-wall")]
        public static void CleaveRush_RejectsBlockedTargetsWithoutSpendingAnOption(string obstacle)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 4, 5);
            switch (obstacle)
            {
                case "strong-fort": game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(8)!.SetPos(target)!); break;
                case "wall-terrain": game.Board.SetTerrain(target, Terrain.Wall); break;
                case "lake": game.Board.SetTerrain(target, Terrain.Lake); break;
                case "line-wall":
                    int index = game.Board.VWallAt(At(game, 4, 4))!.Value;
                    game.Board.VerticalWalls.SetInner(index / 64, 1ul << (index % 64), out _); break;
            }
            Hunt(game);
            Assert.DoesNotContain(target, View(game).CanCleaveRushGrids);
            Assert.False(game.ExecuteAction(KnightCleaveRush.New(0, target)));
            Assert.Equal(0, Role(game).Unit.OptionsUsed);
            Assert.Equal(0, Role(game).Unit.CleaveCooldown);
        }

        [Fact]
        public static void SecretLeap_RequiresALegalJumpAndOnlySharesCoordinatesWithOwner()
        {
            Game game = MakeGame();
            Hunt(game);
            Assert.False(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 4, 4))));
            Assert.False(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 4, 5))));
            Assert.True(game.ExecuteAction(KnightSecretLeap.New(0, At(game, 6, 5))));
            KnightLeapResultEvent result = Assert.Single(game.Events.OfType<KnightLeapResultEvent>());
            Assert.Contains(result, game.GetEventsForPlayer(0));
            Assert.DoesNotContain(result, game.GetEventsForPlayer(1));
            Assert.Single(game.Events.OfType<KnightSecretLeapEvent>());
            Assert.Equal(1, Role(game).Unit.SecretLeapCooldown);
        }

        [Fact]
        public static void CloneAndViews_PreserveChoicesAndCooldownsWithoutSharingArrays()
        {
            Game game = MakeGame();
            KnightUnitView initial = View(game);
            KnightUnitView initialClone = Assert.IsType<KnightUnitView>(initial.Clone());
            initialClone.CanHuntGrids[0] = At(game, 7, 7);
            Assert.DoesNotContain(At(game, 7, 7), initial.CanHuntGrids);
            Hunt(game);
            Assert.True(game.ExecuteAction(KnightSilence.New(0)));
            Knight clone = Assert.IsType<Knight>(Role(game).Clone());
            KnightUnitView snapshot = View(game);
            KnightUnitView viewClone = Assert.IsType<KnightUnitView>(snapshot.Clone());
            Assert.True(clone.Unit.IsHunting);
            Assert.Equal(1, clone.Unit.OptionsUsed);
            Assert.Equal(KnightOption.Silence, clone.Unit.LastOption);
            Assert.Equal(1, clone.Unit.SilenceCooldown);
            Assert.Equal(1, viewClone.SilenceCooldown);
            Assert.False(viewClone.CanSilence);
            viewClone.CanCleaveGrids[0] = At(game, 7, 7);
            viewClone.CanCleaveRushGrids[0] = At(game, 7, 7);
            viewClone.CanSecretLeapGrids[0] = At(game, 7, 7);
            Assert.DoesNotContain(At(game, 7, 7), snapshot.CanCleaveGrids);
            Assert.DoesNotContain(At(game, 7, 7), snapshot.CanCleaveRushGrids);
            Assert.DoesNotContain(At(game, 7, 7), snapshot.CanSecretLeapGrids);
            Assert.True(game.ExecuteAction(KnightCleave.New(0, At(game, 4, 3))));
            Assert.Equal(1, clone.Unit.OptionsUsed);
            Assert.True(clone.HasActionLeft(game));
            NextRound(game);
            Assert.Equal(1, Role(game).Unit.SilenceCooldown);
            Assert.Equal(1, clone.Unit.SilenceCooldown);
        }

        [Fact]
        public static void ViewAndValidation_AgreeInEveryHuntPhase()
        {
            Game game = MakeGame();
            Check();
            Hunt(game);
            Check();
            Assert.True(game.ExecuteAction(KnightSilence.New(0)));
            Check();
            Assert.True(game.ExecuteAction(KnightCleave.New(0, At(game, 4, 3))));
            Check();
            void Check()
            {
                KnightUnitView view = View(game);
                Assert.Equal(view.CanSilence, Role(game).JudgeAction(KnightSilence.New(0), game));
                for (int row = 1; row <= 7; ++row)
                for (int col = 1; col <= 7; ++col)
                {
                    Board.Node node = At(game, row, col);
                    Assert.Equal(view.CanHuntGrids.Contains(node), Role(game).JudgeAction(KnightHunt.New(0, node), game));
                    Assert.Equal(view.CanCleaveGrids.Contains(node), Role(game).JudgeAction(KnightCleave.New(0, node), game));
                    Assert.Equal(view.CanCleaveRushGrids.Contains(node), Role(game).JudgeAction(KnightCleaveRush.New(0, node), game));
                    Assert.Equal(view.CanSecretLeapGrids.Contains(node), Role(game).JudgeAction(KnightSecretLeap.New(0, node), game));
                }
            }
        }

        [Fact]
        public static void EndDuringHunt_ExitsThePhaseAndSpendsNoUnusedOptionCooldowns()
        {
            Game game = MakeGame();
            Hunt(game);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.False(View(game).IsHunting);
            Assert.False(Role(game).HasActionLeft(game));
            Assert.Equal(0, Role(game).Unit.SilenceCooldown);
            Assert.Equal(0, Role(game).Unit.CleaveCooldown);
            Assert.Equal(0, Role(game).Unit.SecretLeapCooldown);
        }
    }
}
