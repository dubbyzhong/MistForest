using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using Xunit;
using static MistForest.Core.Consts.CombatConsts;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SilenceTests
    {
        private static void Silence(Game game)
        {
            Assert.True(game.ExecuteAction(KnightHunt.New(0, game.GetUnit(0)!.Position)));
            Assert.True(game.ExecuteAction(KnightSilence.New(0)));
        }

        [Theory]
        [InlineData(4, 4, true)]
        [InlineData(3, 4, true)]
        [InlineData(5, 4, true)]
        [InlineData(4, 3, true)]
        [InlineData(4, 5, true)]
        [InlineData(5, 5, false)]
        [InlineData(2, 4, false)]
        public static void Silence_AffectsOnlyTheHuntLandingAndItsFourNeighbors(int row, int col, bool hit)
        {
            Game game = KnightHuntTests.MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.Board.GetGrid(enemy.Position)!.Units.Remove(1);
            enemy.Position = game.Board.At(row, col)!.Value;
            game.Board.GetGrid(enemy.Position)!.Units.Add(1);
            Silence(game);
            Assert.Equal(hit, enemy.IsSilenced);
            Assert.Equal(hit, Assert.Single(game.Events.OfType<KnightSilenceResultEvent>()).Hit);
            Assert.False(game.GetUnit(0)!.IsSilenced);
        }

        [Fact]
        public static void Silence_LastsThroughOwnersTurnAndProtectsTheFollowingTurn()
        {
            Game game = KnightHuntTests.MakeGame("hidden");
            Unit target = game.GetUnit(1)!;
            Silence(game);
            Assert.True(target.IsSilenced);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(target.IsSilenced);
            Assert.False(game.ExecuteAction(HiddenWolfTransform.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.False(target.IsSilenced);
            Assert.True(target.SilenceProtected);
            Assert.False(KnightHuntTests.ApplySilence(target));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(target.SilenceProtected);
            Assert.False(KnightHuntTests.ApplySilence(target));
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.False(target.SilenceProtected);
            Assert.True(KnightHuntTests.ApplySilence(target));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Silence_HiddenWolfStillReceivesItButOnlyWolfFormReportsSuccess(bool wolf)
        {
            Game game = KnightHuntTests.MakeGame("hidden");
            HiddenWolf target = Assert.IsType<HiddenWolf>(game.Roles[1]);
            if (wolf) target.ExecuteAction(HiddenWolfTransform.New(1), game);
            Silence(game);
            Assert.True(target.Unit.IsSilenced);
            KnightSilenceResultEvent result = Assert.Single(game.Events.OfType<KnightSilenceResultEvent>());
            Assert.Equal(wolf, result.Hit);
            Assert.Contains(result, game.GetEventsForPlayer(0));
            Assert.DoesNotContain(result, game.GetEventsForPlayer(1));
            Assert.DoesNotContain(typeof(KnightSilenceResultEvent).GetProperties(), property =>
                property.Name is "Target" or "Position" or "UnitIds");
        }

        [Theory]
        [InlineData("alpha")]
        [InlineData("ink")]
        [InlineData("armiger")]
        [InlineData("heavy")]
        [InlineData("shadow")]
        [InlineData("hidden")]
        [InlineData("knight")]
        public static void Silence_BlocksSkillsInBothViewAndValidationButKeepsBasicActions(string kind)
        {
            Game game = KnightHuntTests.MakeGame(kind);
            Role role = game.Roles[1];
            Unit target = game.GetUnit(1)!;
            if (kind == "armiger") typeof(Unit).GetProperty(nameof(Unit.Armor))!.SetValue(target, UnitsPerPoint);
            Silence(game);
            Assert.True(target.IsSilenced);
            Assert.True(role.JudgeAction(GeneralEnd.New(1), game));
            Assert.True(role.JudgeAction(GeneralSurrender.New(1), game));
            switch (kind)
            {
                case "alpha":
                    Assert.False(role.JudgeAction(AlphaBlood.New(1), game));
                    Assert.False(Assert.IsType<AlphaUnitView>(target.GetView(game)).CanBlood);
                    break;
                case "ink":
                    Assert.False(role.JudgeAction(InkDash.New(1, game.Board.At(2, 5)!.Value), game));
                    Assert.Empty(Assert.IsType<InkUnitView>(target.GetView(game)).CanDashGrids);
                    Assert.True(role.JudgeAction(InkAttack.New(1, target.Position), game));
                    break;
                case "armiger":
                    Assert.False(role.JudgeAction(ArmigerArmorUp.New(1), game));
                    Assert.False(role.JudgeAction(ArmigerArmorDown.New(1, UnitsPerPoint), game));
                    ArmigerUnitView armor = Assert.IsType<ArmigerUnitView>(target.GetView(game));
                    Assert.False(armor.CanArmorUp);
                    Assert.Empty(armor.CanArmorDownAmounts);
                    Assert.NotEmpty(armor.CanThrustGrids);
                    break;
                case "heavy":
                    Assert.False(role.JudgeAction(HeavyChargeStart.New(1, game.Board.At(3, 5)!.Value), game));
                    Assert.Empty(Assert.IsType<HeavyUnitView>(target.GetView(game)).CanChargeStartGrids);
                    break;
                case "shadow":
                    Assert.False(role.JudgeAction(ShadowFollow.New(1, 2), game));
                    Assert.False(role.JudgeAction(ShadowDrag.New(1, 2), game));
                    ShadowUnitView shadow = Assert.IsType<ShadowUnitView>(target.GetView(game));
                    Assert.Empty(shadow.CanFollowUnitIds);
                    Assert.Empty(shadow.CanDragUnitIds);
                    break;
                case "hidden":
                    Assert.False(role.JudgeAction(HiddenWolfTransform.New(1), game));
                    Assert.False(Assert.IsType<HiddenWolfUnitView>(target.GetView(game)).CanTransform);
                    Assert.True(role.JudgeAction(HiddenWolfAttack.New(1, target.Position), game));
                    break;
                case "knight":
                    KnightUnitView knight = Assert.IsType<KnightUnitView>(target.GetView(game));
                    Assert.Empty(knight.CanHuntGrids);
                    Assert.NotEmpty(knight.CanLeapGrids);
                    Assert.True(role.JudgeAction(KnightLeap.New(1, knight.CanLeapGrids[0]), game));
                    return;
            }
            Assert.NotEmpty(target.GetView(game).CanMoveGrids);
        }

        [Fact]
        public static void Silence_IgnoresBuildingsAndEdgesAndDoesNotRevealProtectedTargets()
        {
            Game game = KnightHuntTests.MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(UnitsPerPoint)!.SetPos(enemy.Position)!);
            int index = game.Board.VWallAt(game.GetUnit(0)!.Position)!.Value;
            game.Board.VerticalWalls.SetInner(index / 64, 1ul << (index % 64), out _);
            Silence(game);
            Assert.True(enemy.IsSilenced);
            Assert.True(Assert.Single(game.Events.OfType<KnightSilenceResultEvent>()).Hit);

            game = KnightHuntTests.MakeGame();
            enemy = game.GetUnit(1)!;
            Assert.True(KnightHuntTests.ApplySilence(enemy));
            enemy.EndTurn(game);
            Silence(game);
            Assert.False(enemy.IsSilenced);
            Assert.False(Assert.Single(game.Events.OfType<KnightSilenceResultEvent>()).Hit);
        }

        [Fact]
        public static void Silence_ClonesPreserveStateAndRoundResetDoesNotEndTheEffect()
        {
            Game game = KnightHuntTests.MakeGame("hidden");
            Silence(game);
            HiddenWolf clone = Assert.IsType<HiddenWolf>(game.Roles[1].Clone());
            Assert.True(clone.Unit.IsSilenced);
            clone.Unit.EndRound(game);
            Assert.True(clone.Unit.IsSilenced);
            clone.Unit.PrepareTurn(game);
            Assert.True(clone.Unit.IsSilenced);
            clone.Unit.EndTurn(game);
            Assert.False(clone.Unit.IsSilenced);
            Assert.True(clone.Unit.SilenceProtected);
            Assert.True(game.GetUnit(1)!.IsSilenced);
            HiddenWolf protectedClone = Assert.IsType<HiddenWolf>(clone.Clone());
            Assert.True(protectedClone.Unit.SilenceProtected);
            Assert.False(KnightHuntTests.ApplySilence(protectedClone.Unit));
        }

        [Fact]
        public static void Silence_PausesShadowLinkUntilItsNextTurnWithoutExtendingExpiry()
        {
            Game game = KnightHuntTests.MakeGame("shadow");
            Shadow shadow = Assert.IsType<Shadow>(game.Roles[1]);
            // Prepare an existing link and exercise copied-step hooks independently of turn ownership.
            shadow.ExecuteAction(ShadowFollow.New(1, 2), game);
            Silence(game);
            Board.Node from = shadow.Unit.Position;
            Role target = game.Roles[2];
            GeneralMove move = GeneralMove.New(2, game.Board.At(7, 6)!.Value);
            Assert.True(target.JudgeAction(move, game));
            target.ExecuteAction(move, game);
            Assert.Equal(from, shadow.Unit.Position);
            Assert.Equal(2, shadow.Unit.TargetUnitId);
            Shadow clone = Assert.IsType<Shadow>(shadow.Clone());
            Assert.True(clone.Unit.IsSilenced);
            Assert.Equal(2, clone.Unit.TargetUnitId);

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.False(shadow.Unit.IsSilenced);
            Assert.True(shadow.Unit.SilenceProtected);
            Assert.Equal(2, shadow.Unit.TargetUnitId);
            move = GeneralMove.New(2, game.Board.At(7, 5)!.Value);
            Assert.True(target.JudgeAction(move, game));
            target.ExecuteAction(move, game);
            Assert.Equal(game.Board.At(4, 4), shadow.Unit.Position);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Null(shadow.Unit.TargetUnitId);
        }

        [Fact]
        public static void SilencedKnight_CannotUseRemainingHuntSkillsButCanEnd()
        {
            Game game = KnightHuntTests.MakeGame();
            Assert.True(game.ExecuteAction(KnightHunt.New(0, game.GetUnit(0)!.Position)));
            Assert.True(KnightHuntTests.ApplySilence(game.GetUnit(0)!));
            KnightUnitView view = Assert.IsType<KnightUnitView>(game.GetUnit(0)!.GetView(game));
            Assert.False(view.CanSilence);
            Assert.Empty(view.CanCleaveGrids);
            Assert.Empty(view.CanSecretLeapGrids);
            Assert.False(game.ExecuteAction(KnightSilence.New(0)));
            Assert.False(game.ExecuteAction(KnightCleave.New(0, game.GetUnit(1)!.Position)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
        }
    }
}
