using Map = MistForest.Core.Model.Map;
using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class KnightAlphaTests
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

        private static GameAction Do(int unit, ActionKind kind, int target, int amount = 1)
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Amount = amount,
            };
        }

        [Fact]
        public static void Knight_Leap_BasicAndHoofBlock()
        {
            Game game = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.Leap, 13)));
            Assert.Equal(13, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Turn);

            Game game2 = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            game2.State.Map.Cells[6] = Terrain.Wall;
            Assert.False(game2.Act(Do(0, ActionKind.Leap, 13)));

            Game game3 = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            Assert.True(game3.Act(Do(0, ActionKind.Leap, 2)));
            Assert.Equal(1, game.State.Turn);
        }

        [Fact]
        public static void Knight_Hunt_DamagesAndOpensOptions()
        {
            Game game = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            game.State.Units[2].Position = 13;
            Assert.True(game.Act(Do(0, ActionKind.Hunt, 13)));
            Assert.Equal(13, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Units[2].Health);
            Assert.Equal(3, game.State.Budget.Phase);
            Assert.Equal(13, game.State.Budget.Anchor);
            Assert.True(game.State.Possible[1][0][12]);
            int total = 0;
            foreach (bool b in game.State.Possible[1][0])
            {
                if (b)
                {
                    ++total;
                }
            }
            Assert.Equal(1, total);
            Assert.Equal(0, game.State.Turn);
        }

        [Fact]
        public static void Knight_Options_OrderAndCooldowns()
        {
            Game game = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            game.State.Units[2].Position = 20;
            Assert.True(game.Act(Do(0, ActionKind.Hunt, 13)));
            Assert.False(game.Act(Do(0, ActionKind.Hunt, 7)));
            Assert.True(game.Act(Do(0, ActionKind.Retreat, 2)));
            Assert.Equal(2, game.State.Units[0].OptionCooldown[2]);
            Assert.False(game.Act(Do(0, ActionKind.Cleave, 3)));
            game.State.Budget.Phase = 3;
            game.State.Budget.LastOption = 0;
            game.State.Budget.Options = 1;
            Assert.True(game.Act(Do(0, ActionKind.Silence, 0)));
            Assert.Equal(2, game.State.Budget.Options);
            Assert.False(game.Act(Do(0, ActionKind.Cleave, 3)));
            Assert.Equal(2, game.State.Units[0].OptionCooldown[0]);
            Assert.Equal(0, game.State.Units[0].OptionCooldown[1]);
        }

        [Fact]
        public static void Knight_Silence_AreaAndCandidateNarrowing()
        {
            Game game = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            game.State.Units[2].Position = 20;
            Assert.True(game.Act(Do(0, ActionKind.Hunt, 13)));
            game.State.Units[2].Position = 12;
            game.State.Units[3].Health = 0;
            Assert.True(game.Act(Do(0, ActionKind.Silence, 0)));
            Assert.True(game.State.Units[2].Silenced);
            Assert.True(game.State.Possible[0][2][11]);
            Assert.False(game.State.Possible[0][2][22 - 1]);
        }

        [Fact]
        public static void Knight_CleaveRush_DamageThenMoveIn()
        {
            Game game = NewDuel(Role.Knight, Role.Wolf, 2, 22);
            game.State.Units[2].Position = 12;
            Assert.True(game.Act(Do(0, ActionKind.Hunt, 13)));
            Assert.True(game.Act(Do(0, ActionKind.CleaveRush, 12)));
            Assert.Equal(12, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Units[2].Health);
            Assert.Equal(2, game.State.Units[0].OptionCooldown[1]);
        }

        [Fact]
        public static void Alpha_MoveTwice_AttackRush_Blood()
        {
            Game game = NewDuel(Role.Alpha, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.Move, 3)));
            Assert.True(game.Act(Do(0, ActionKind.Move, 4)));
            Assert.False(game.Act(Do(0, ActionKind.Move, 5)));
            game.State.Units[2].Position = 5;
            Assert.False(game.Act(Do(0, ActionKind.Attack, 5)));

            Game game2 = NewDuel(Role.Alpha, Role.Wolf, 2, 22);
            game2.State.Units[2].Position = 7;
            Assert.True(game2.Act(Do(0, ActionKind.AttackRush, 7)));
            Assert.Equal(7, game2.State.Units[0].Position);
            Assert.Equal(1, game2.State.Units[2].Health);
            Assert.Equal(1, game2.State.Turn);

            Game game3 = NewDuel(Role.Alpha, Role.Wolf, 2, 22);
            game3.State.Units[2].Position = 10;
            game3.State.Units[3].Position = 20;
            Assert.True(game3.Act(Do(0, ActionKind.Blood, 0)));
            Assert.Equal(3, game3.State.Units[0].Cooldown);
            Assert.True(game3.State.Intel[1][1]);
            Assert.Contains("10", game3.Events[0].PrivateText);
            Assert.False(game3.State.Possible[0][2][6]);
            Assert.True(game3.State.Possible[0][2][9]);
            Assert.False(game3.Act(Do(0, ActionKind.Blood, 0)));
            Assert.True(game3.Act(Do(0, ActionKind.Move, 3)));
        }
    }
}
