using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class PrinceSwordFlameTests
    {
        private static Game NewDuel(Role first, Role second, int posFirst, int posSecond)
        {
            return new Game(
                new int[] { posFirst, 9, posSecond, 4, 0, 0, 0, 0 },
                new Role[]
                {
                    first,
                    Role.Sniper,
                    second,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                },
                Mode.Heroes,
                0,
                new Map(),
                2,
                2,
                false
            );
        }

        private static GameAction Do(
            int unit,
            ActionKind kind,
            int target,
            int? subject = null,
            int amount = 1,
            int extra = 0,
            int landing = 0
        )
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Subject = subject,
                Amount = amount,
                Extra = extra,
                Landing = landing,
            };
        }

        [Fact]
        public static void Prince_PlantSharesMove_NoNormalAttack()
        {
            Game game = NewDuel(Role.Prince, Role.Wolf, 7, 22);
            Assert.False(game.Act(Do(0, ActionKind.Attack, 8)));
            Assert.True(game.Act(Do(0, ActionKind.PlantRose, 9)));
            Assert.Single(game.State.Roses);
            Assert.Equal(3, game.State.Roses[0].Health);
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Budget.Moves);
            Assert.Equal(0, game.State.Turn); // planting replaces the move, no end of turn
            Assert.False(game.Act(Do(0, ActionKind.PlantRose, 12)));
            Assert.False(game.Act(Do(0, ActionKind.Move, 8)));
        }

        [Fact]
        public static void Prince_Bloom_BlinksDropsPetal_OncePerRosePerTurn()
        {
            Game game = NewDuel(Role.Prince, Role.Wolf, 7, 12);
            Assert.True(game.Act(Do(0, ActionKind.PlantRose, 8)));
            Assert.True(game.Act(Do(0, ActionKind.End, 0)));
            Assert.Equal(1, game.State.Turn);
            Assert.True(game.Act(Do(2, ActionKind.End, 0)));
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.PlantRose, 9)));

            Rose rose = game.State.Roses[0];
            GameAction bloom = Do(0, ActionKind.Bloom, 12, extra: 8, landing: 7);
            Assert.True(game.Act(bloom));
            Assert.Equal(2, rose.Health);
            Assert.Equal(1, game.State.Units[2].Health);
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.Equal(0, game.State.Turn);
            Assert.Single(game.State.Budget.RoseUsed);
            Assert.False(game.Act(bloom));
        }

        [Fact]
        public static void Prince_Rose_TakesIndependentDamage_NoStacking()
        {
            Game game = NewDuel(Role.Prince, Role.Wolf, 7, 13);
            Assert.True(game.Act(Do(0, ActionKind.PlantRose, 8)));
            Assert.True(game.Act(Do(0, ActionKind.End, 0)));
            Assert.Equal(1, game.State.Turn);
            Assert.True(game.Act(Do(2, ActionKind.Attack, 8)));
            Assert.Equal(1, game.State.Roses[0].Health);
            Assert.Equal(3, game.State.Units[0].Health);
            Assert.True(game.Act(Do(2, ActionKind.Attack, 8)));
            Assert.Equal(1, game.State.Roses[0].Health);
        }

        [Fact]
        public static void Sword_ThrowQiCap_RecallExtraStab_Advance()
        {
            Game game = NewDuel(Role.Sword, Role.Wolf, 7, 22);
            Assert.False(game.Act(Do(0, ActionKind.Attack, 8)));
            Assert.True(game.Act(Do(0, ActionKind.ThrowSword, 9, amount: 2)));
            Assert.Single(game.State.Swords);
            Assert.Equal(9, game.State.Swords[0].Cell);
            Assert.Equal(1, game.State.Turn);
            Assert.Equal(8, game.State.Units[0].SwordQi);

            Assert.True(game.Act(Do(2, ActionKind.End, 0)));
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.RecallSword, 9)));
            Assert.Empty(game.State.Swords);
            Assert.Equal(6, game.State.Units[0].SwordQi);
            Assert.Equal(1, game.State.Budget.ExtraAttacks);
            Assert.Equal(0, game.State.Turn);

            Assert.True(game.Act(Do(0, ActionKind.SwordStab, 8, amount: 0)));
            Assert.Equal(5, game.State.Units[0].SwordQi);
            Assert.True(game.State.Budget.SwordAdvance);
            Assert.True(game.State.Budget.SwordCells[7]);
            Assert.True(game.State.Budget.SwordCells[8]);
            Assert.Equal(1, game.State.Budget.Attacks);
            Assert.Equal(1, game.State.Budget.ExtraAttacks);
            Assert.False(game.Act(Do(0, ActionKind.Move, 6)));
            Assert.True(game.Act(Do(0, ActionKind.SwordAdvance, 9)));
            Assert.Equal(9, game.State.Units[0].Position);
            Assert.False(game.State.Budget.SwordAdvance);

            Assert.True(game.Act(Do(0, ActionKind.SwordStab, 14, amount: 0)));
            Assert.Equal(4, game.State.Units[0].SwordQi);
            Assert.Equal(0, game.State.Budget.ExtraAttacks);
            Assert.True(game.Act(Do(0, ActionKind.SwordAdvance, 19)));
            Assert.Equal(19, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Turn);
        }

        [Fact]
        public static void Sword_MoveAndStab_MutuallyExclusive()
        {
            Game game = NewDuel(Role.Sword, Role.Wolf, 7, 22);
            Assert.True(game.Act(Do(0, ActionKind.Move, 8)));
            Assert.Equal(8, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Turn);
            Assert.False(game.Act(Do(0, ActionKind.SwordStab, 9, amount: 0)));

            Game game2 = NewDuel(Role.Sword, Role.Wolf, 7, 22);
            Assert.True(game2.Act(Do(0, ActionKind.SwordStab, 8, amount: 0)));
            Assert.True(game2.Act(Do(0, ActionKind.SwordAdvance, 0)));
            Assert.False(game2.Act(Do(0, ActionKind.Move, 6)));
            Assert.False(game2.Act(Do(0, ActionKind.SwordStab, 9, amount: 0)));
        }

        [Fact]
        public static void Firefly_Flame_SpendsAmmo_OpensFollowUp()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 7, 22);
            Assert.False(game.Act(Do(0, ActionKind.Attack, 8)));
            Assert.True(game.Act(Do(0, ActionKind.Flame, 8, amount: 2)));
            Assert.Equal(1, game.State.Units[0].Ammo);
            Assert.Equal(1, game.State.Budget.Used);
            Assert.True(game.State.Budget.FlameChoice);
            Assert.NotEqual(0, game.State.Fires[7].Expires);
            Assert.NotEqual(0, game.State.Fires[8].Expires);
            Assert.False(game.Act(Do(0, ActionKind.Move, 6)));
            Assert.False(game.Act(Do(0, ActionKind.End, 0)));
            Assert.True(game.Act(Do(0, ActionKind.FlameAdvance, 9)));
            Assert.Equal(9, game.State.Units[0].Position);
            Assert.False(game.State.Budget.FlameChoice);
            Assert.True(game.State.Budget.Advance);
            Assert.False(game.Act(Do(0, ActionKind.FlameAdvance, 0)));
            Assert.True(game.Act(Do(0, ActionKind.FireStep, 4, amount: 0)));
            Assert.Equal(1, game.State.Units[0].Ammo);
            Assert.Equal(1, game.State.Turn);
        }

        [Fact]
        public static void Firefly_SilenceBlocksSkills_NotMovement()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 7, 22);
            game.State.Units[0].Silenced = true;
            Assert.False(game.Act(Do(0, ActionKind.Flame, 8, amount: 1)));
            Assert.False(game.Act(Do(0, ActionKind.FireStep, 8, amount: 0)));
            Assert.True(game.Act(Do(0, ActionKind.Move, 8)));
            Assert.Equal(8, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Turn);
        }
    }
}
