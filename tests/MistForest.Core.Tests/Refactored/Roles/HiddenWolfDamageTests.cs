using System.Reflection;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class HiddenWolfDamageTests
    {
        private static Game MakeGame(string attacker = "ink", bool wolf = false, bool shield = false)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node from = board.At(3, attacker == "heavy" ? 5 : 4)!.Value;
            RoleBuilder attackerBuilder = attacker switch
            {
                "wolf" => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, from)!,
                "alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, from)!,
                "armiger" => ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, from)!,
                "heavy" => HeavyBuilder.New(new UnitBuilder[] { new HeavyUnitBuilder() }, from)!,
                "sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, from)!,
                "hidden-human" or "hidden-wolf" => HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, from)!,
                _ => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, from)!,
            };
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board))!.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder> { attackerBuilder })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
            }));
            Game game = builder.Build()!;
            if (wolf || shield)
            {
                Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            }
            if (shield)
            {
                EndRound(game);
                Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            }
            return game;
        }

        private static HiddenWolf Role(Game game) => Assert.IsType<HiddenWolf>(game.Roles[0]);
        private static HiddenWolfUnit Unit(Game game) => Role(game).Unit;

        private static void EndRound(Game game)
        {
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
        }

        // Synthetic strengths exercise the internal pipeline beyond the current roles' attack values.
        private static DamageResult Hit(Unit unit, Game game, int strength, DamageType type)
            => (DamageResult)typeof(Unit).GetMethod("ReceiveDirectDamage",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(unit, new object[] { strength, game, type })!;

        private static void SetDefense(Unit unit, string property, int value)
            => typeof(Unit).GetProperty(property)!.SetValue(unit, value);

        [Theory]
        [InlineData("ink", false, 5)]
        [InlineData("ink", true, 4)]
        [InlineData("wolf", false, 2)]
        [InlineData("wolf", true, 4)]
        [InlineData("alpha", false, 2)]
        [InlineData("alpha", true, 4)]
        [InlineData("armiger", false, 2)]
        [InlineData("armiger", true, 4)]
        [InlineData("heavy", false, 2)]
        [InlineData("heavy", true, 4)]
        [InlineData("hidden-human", false, 4)]
        [InlineData("hidden-human", true, 2)]
        [InlineData("hidden-wolf", false, 2)]
        [InlineData("hidden-wolf", true, 4)]
        public static void GameEntry_ClassifiesAttacksAndAppliesTheCurrentFormsReduction(
            string attacker, bool wolf, int health)
        {
            Game game = MakeGame(attacker, wolf);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Board.Node target = Unit(game).Position;
            if (attacker == "hidden-wolf")
            {
                Assert.True(game.ExecuteAction(HiddenWolfTransform.New(1)));
            }
            if (attacker == "heavy")
            {
                Assert.True(game.ExecuteAction(HeavyChargeStart.New(1, game.Board.At(3, 4)!.Value)));
            }
            GameAction attack = attacker switch
            {
                "wolf" => WolfBite.New(1, target),
                "alpha" => AlphaAttack.New(1, target),
                "armiger" => ArmigerThrust.New(1, target),
                "heavy" => HeavyChargeContinue.New(1, target),
                "hidden-human" or "hidden-wolf" => HiddenWolfAttack.New(1, target),
                _ => InkAttack.New(1, target),
            };
            Assert.True(game.ExecuteAction(attack));
            Assert.Equal(health, Unit(game).Health);
            Assert.Equal(6 - health, Unit(game).HighestDirectDamage);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void HighestDamage_ComparesReducedStrengthBeforeTakingTheMaximum(bool reducedFirst)
        {
            Game game = MakeGame();
            HiddenWolfUnit unit = Unit(game);
            SetDefense(unit, nameof(HiddenWolfUnit.Health), 40);
            DamageResult first = reducedFirst
                ? Hit(unit, game, 12, DamageType.Ranged) // 3 points reduced to 1.5.
                : Hit(unit, game, 8, DamageType.Melee);  // 2 points without reduction.
            Assert.Equal(reducedFirst ? 6 : 8, first.HealthDamage);
            Assert.Equal(reducedFirst ? 6 : 8, unit.HighestDirectDamage);
            DamageResult second = reducedFirst
                ? Hit(unit, game, 8, DamageType.Melee)
                : Hit(unit, game, 12, DamageType.Ranged);
            Assert.Equal(reducedFirst ? 2 : 0, second.TotalDamage);
            Assert.Equal(32, unit.Health);
            Assert.Equal(8, unit.HighestDirectDamage);
            Assert.Equal(0, Hit(unit, game, 12, DamageType.Ranged).TotalDamage);
            Assert.Equal(32, unit.Health);
        }

        [Fact]
        public static void Reduction_UsesTheFormAtImpactWithoutRewritingPreviousDamage()
        {
            Game game = MakeGame();
            Assert.Equal(1, Hit(Unit(game), game, 2, DamageType.Ranged).HealthDamage);
            Assert.Equal(1, Unit(game).HighestDirectDamage);
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Assert.Equal(1, Hit(Unit(game), game, 4, DamageType.Melee).HealthDamage);
            Assert.Equal(2, Unit(game).HighestDirectDamage);
            Assert.Equal(0, Hit(Unit(game), game, 2, DamageType.Ranged).TotalDamage);
            Assert.Equal(4, Unit(game).Health);
        }

        [Theory]
        [InlineData(1, 1, 0, 0, 6)]
        [InlineData(3, 2, 1, 0, 6)]
        [InlineData(7, 2, 2, 3, 3)]
        [InlineData(12, 2, 2, 6, 0)]
        public static void Shield_ResolvesAfterArmorBeforeHealthAndReportsActualLoss(
            int strength, int armorDamage, int shieldDamage, int healthDamage, int health)
        {
            Game game = MakeGame(shield: true);
            SetDefense(Unit(game), nameof(HiddenWolfUnit.Armor), 2);
            DamageResult result = Hit(Unit(game), game, strength, DamageType.Melee);
            Assert.Equal(armorDamage, result.ArmorDamage);
            Assert.Equal(shieldDamage, result.ShieldDamage);
            Assert.Equal(healthDamage, result.HealthDamage);
            Assert.Equal(armorDamage + shieldDamage + healthDamage, result.TotalDamage);
            Assert.Equal(health, Unit(game).Health);
            Assert.Equal(health > 0, Unit(game).Alive);
            if (health == 0)
            {
                Assert.Equal(0, Unit(game).Armor);
                Assert.Equal(0, Unit(game).TemporaryShield);
                Assert.DoesNotContain(0, game.Board.GetGrid(Unit(game).Position)!.Units);
            }
            else
            {
                Assert.Equal(2 - armorDamage, Unit(game).Armor);
                Assert.Equal(2 - shieldDamage, Unit(game).TemporaryShield);
            }
        }

        [Fact]
        public static void Shield_ReductionAndHighestStrengthPreventRepeatedShieldConsumption()
        {
            Game game = MakeGame(shield: true);
            Assert.Equal(1, Hit(Unit(game), game, 2, DamageType.Ranged).ShieldDamage);
            Assert.Equal(1, Unit(game).TemporaryShield);
            Assert.Equal(1, Unit(game).HighestDirectDamage);
            Assert.Equal(0, Hit(Unit(game), game, 2, DamageType.Ranged).TotalDamage);
            Assert.Equal(1, Hit(Unit(game), game, 4, DamageType.Ranged).ShieldDamage);
            Assert.Equal(0, Unit(game).TemporaryShield);
            Assert.Equal(6, Unit(game).Health);
            Assert.Equal(2, Unit(game).HighestDirectDamage);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public static void NonPositiveDamage_DoesNotChangeShieldOrHistory(int strength)
        {
            Game game = MakeGame(shield: true);
            Assert.Equal(0, Hit(Unit(game), game, strength, DamageType.Melee).TotalDamage);
            Assert.Equal(2, Unit(game).TemporaryShield);
            Assert.Equal(6, Unit(game).Health);
            Assert.Equal(0, Unit(game).HighestDirectDamage);
        }

        [Fact]
        public static void ShieldClone_PreservesPartialShieldAndEffectiveDamageWithoutSharingState()
        {
            Game game = MakeGame(shield: true);
            Hit(Unit(game), game, 2, DamageType.Ranged);
            HiddenWolf clone = Assert.IsType<HiddenWolf>(Role(game).Clone());
            HiddenWolfUnitView snapshot = Assert.IsType<HiddenWolfUnitView>(Unit(game).GetView(game));
            HiddenWolfUnitView viewClone = Assert.IsType<HiddenWolfUnitView>(snapshot.Clone());
            Hit(Unit(game), game, 4, DamageType.Melee);
            Assert.Equal(0, Unit(game).TemporaryShield);
            Assert.Equal(4, Unit(game).Health);
            Assert.Equal(1, clone.Unit.TemporaryShield);
            Assert.Equal(6, clone.Unit.Health);
            Assert.Equal(1, clone.Unit.HighestDirectDamage);
            Assert.Equal(1, snapshot.TemporaryShield);
            Assert.Equal(1, viewClone.TemporaryShield);
            Assert.Equal(HiddenWolfForm.Human, clone.Unit.Form);
            Assert.True(clone.HasStartedTurn);
            Assert.True(clone.IsFirstAction);
            clone.Unit.EndRound(game);
            Assert.Equal(1, clone.Unit.TemporaryShield);
            Assert.Equal(0, clone.Unit.HighestDirectDamage);
            clone.Unit.PrepareTurn(game);
            Assert.Equal(0, clone.Unit.TemporaryShield);
            Assert.False(clone.Unit.HasTransformedThisTurn);
        }

        [Fact]
        public static void Ink_ActualShieldLossRefreshesDashButCoveredAttacksDoNot()
        {
            Game game = MakeGame(shield: true);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            InkUnit attacker = Assert.IsType<Ink>(game.Roles[1]).Unit;
            Board.Node from = attacker.Position;
            Assert.True(game.ExecuteAction(InkDash.New(1, game.Board.At(2, 3)!.Value)));
            Assert.Equal(2, attacker.DashCooldown);
            Assert.True(game.ExecuteAction(InkAttack.New(1, Unit(game).Position)));
            Assert.Equal(0, attacker.DashCooldown);
            Assert.Equal(6, Unit(game).Health);
            Assert.Equal(1, Unit(game).TemporaryShield);
            Assert.True(game.ExecuteAction(InkDash.New(1, from)));
            Assert.True(game.ExecuteAction(InkAttack.New(1, Unit(game).Position)));
            Assert.Equal(2, attacker.DashCooldown);
            Assert.Equal(1, Unit(game).TemporaryShield);
            Assert.Equal(6, Unit(game).Health);
        }

        [Fact]
        public static void Shield_ExpiresAtOwnersNextTurnNotAtGlobalRoundBoundary()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board))!.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 4)!.Value)!,
                })!,
            }));
            Game game = builder.Build()!;
            HiddenWolfUnit hidden = Assert.IsType<HiddenWolf>(game.Roles[1]).Unit;
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(1)));
            Assert.Equal(2, hidden.TemporaryShield);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(2, hidden.TemporaryShield);
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(2, hidden.TemporaryShield);
            Assert.True(hidden.HasTransformedThisTurn);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(0, hidden.TemporaryShield);
            Assert.False(hidden.HasTransformedThisTurn);
        }

        [Fact]
        public static void SniperExecution_BypassesFormReductionAndClearsTemporaryShield()
        {
            Game game = MakeGame("sniper", shield: true);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Board.Node position = Unit(game).Position;
            Assert.True(game.ExecuteAction(SniperExecute.New(1, position)));
            Assert.False(Unit(game).Alive);
            Assert.Equal(0, Unit(game).Health);
            Assert.Equal(0, Unit(game).TemporaryShield);
            Assert.DoesNotContain(0, game.Board.GetGrid(position)!.Units);
        }
    }
}
