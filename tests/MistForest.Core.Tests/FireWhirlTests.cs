using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class FireWhirlTests
    {
        private static Game NewDuel(
            Role first,
            Role second,
            int posFirst,
            int posSecond,
            Map? map = null
        )
        {
            Role partnerFirst = first == Role.Sniper ? Role.Wolf : Role.Sniper;
            Role partnerSecond = second == Role.Sniper ? Role.Wolf : Role.Sniper;
            return new Game(
                new int[] { posFirst, 9, posSecond, 4, 0, 0, 0, 0 },
                new Role[]
                {
                    first,
                    partnerFirst,
                    second,
                    partnerSecond,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                },
                Mode.Heroes,
                0,
                map,
                2,
                2,
                false
            );
        }

        private static GameAction Do(int unit, ActionKind kind, int target, int amount = 0)
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Amount = amount,
            };
        }

        private static void EndCurrent(Game game)
        {
            game.Act(new GameAction { Unit = game.State.Turn * 2, Kind = ActionKind.End });
        }

        [Fact]
        public static void FireStep_PureMove_IsFree_CanStillMove()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.FireStep, 7, 0)));
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.Equal(2, game.State.Units[0].Ammo);
            Assert.Equal(0, game.State.Fires[6].Expires);
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.Move, 8)));
            Assert.Equal(1, game.State.Turn);
        }

        [Fact]
        public void FireStep_Ignite_DirectFlameAndAmmoBlock()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.FireStep, 7, 1)));
            Assert.Equal(1, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Fires[6].Owner);
            Assert.Equal(1, game.State.Fires[6].Expires);
            Assert.True(game.State.Units[0].AmmoBlocked);
            EndCurrent(game);
            Assert.Equal(2, game.State.Units[0].Ammo);
            Assert.False(game.State.Units[0].AmmoBlocked);
        }

        [Fact]
        public void Flamer_NormalMove_RegainsAmmoNextTurn()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 2, 22);
            game.Act(Do(0, ActionKind.FireStep, 7, 0));
            EndCurrent(game);
            Assert.Equal(3, game.State.Units[0].Ammo);
        }

        [Fact]
        public static void Burning_ResolvesAtVictimOwnerTurnEnd_BypassesObjectCap()
        {
            Game game = NewDuel(Role.Firefly, Role.Heavy, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.FireStep, 7, 1)));
            Assert.Equal(0, game.State.Units[2].Armor);
            Assert.Equal(4, game.State.Units[2].Health);
            Assert.Equal(2, game.State.Units[2].DirectDamage);
            EndCurrent(game);
            Assert.Equal(4, game.State.Units[2].Health);
            EndCurrent(game);
            Assert.Equal(2, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Fires[6].Expires);
        }

        [Fact]
        public static void Flamer_ImmuneToContinuousBurning_NotDirectFlame()
        {
            Game game = NewDuel(Role.Firefly, Role.Firefly, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.FireStep, 7, 1)));
            Assert.Equal(1, game.State.Units[2].Health);
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(1, game.State.Units[2].Health);
        }

        [Fact]
        public static void Fire_ExpiresAtCasterNextTurnStart()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 2, 22);
            game.Act(Do(0, ActionKind.FireStep, 7, 1));
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(0, game.State.Fires[6].Expires);
            Assert.Equal(0, game.State.Turn);
            EndCurrent(game);
            EndCurrent(game);
            Assert.DoesNotContain(
                "Burning resolves",
                game.Events[game.Events.Count - 1].PublicText
            );
        }

        [Fact]
        public static void EnemyFire_BurnsYouAtYourOwnTurnEnd()
        {
            Game game = new Game(
                new int[] { 2, 9, 3, 22, 0, 0, 0, 0 },
                new Role[]
                {
                    Role.Wolf,
                    Role.Sniper,
                    Role.Firefly,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                    Role.Sniper,
                },
                Mode.Heroes,
                0,
                null,
                2,
                2,
                false
            );
            EndCurrent(game);
            Assert.True(game.Act(Do(2, ActionKind.FireStep, 2, 1)));
            Assert.Equal(1, game.State.Units[0].Health);
            EndCurrent(game);
            Assert.Equal(1, game.State.Units[0].Health);
            EndCurrent(game);
            Assert.Equal(0, game.State.Units[0].Health);
            Assert.Contains("Burning resolves at 2", game.Events[game.Events.Count - 1].PublicText);
        }

        [Fact]
        public static void Flamer_StandingOnFire_RegainsTwoAmmo()
        {
            Game game = new Game(
                new int[] { 2, 9, 3, 22, 0, 0, 0, 0 },
                new Role[] { Role.Firefly, Role.Sniper, Role.Firefly, Role.Sniper, Role.Sniper, Role.Sniper, Role.Sniper, Role.Sniper },
                Mode.Heroes, 0, null, 2, 2, false);
            EndCurrent(game);
            Assert.True(game.Act(Do(2, ActionKind.FireStep, 2, 1)));
            Assert.Equal(1, game.State.Units[0].Health);
            game.State.Units[0].Ammo = 1;
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(3, game.State.Units[0].Ammo);
            Assert.Equal(1, game.State.Units[0].Health);
        }

        [Fact]
        public static void Whirlpool_ConstructPull_ConsumeImmunity_PullAgainNextRound()
        {
            var map = new Map();
            map.Cells[6] = Terrain.Whirlpool;
            Game game = NewDuel(Role.Wolf, Role.Sniper, 2, 22, map);
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.True(game.State.Units[0].WhirlImmune);
            game.Act(Do(0, ActionKind.Move, 6));
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(6, game.State.Units[0].Position);
            Assert.False(game.State.Units[0].WhirlImmune);
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.True(game.State.Units[0].WhirlImmune);
        }

        [Fact]
        public static void Whirlpool_NoAdjacent_NoPull()
        {
            var map = new Map();
            map.Cells[6] = Terrain.Whirlpool;
            Game game = NewDuel(Role.Sniper, Role.Sniper, 3, 23, map);
            Assert.Equal(3, game.State.Units[0].Position);
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(3, game.State.Units[0].Position);
        }
    }
}
