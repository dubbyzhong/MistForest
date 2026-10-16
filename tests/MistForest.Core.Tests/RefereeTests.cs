using Map = MistForest.Core.Model.Map;
using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class RefereeTests
    {
        private static Game NewDuel(
            Role first,
            Role second,
            int posFirst,
            int posSecond,
            Mode mode = Mode.Heroes,
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
                mode,
                0,
                map,
                2,
                2,
                false
            );
        }

        private static GameAction Do(int unit, ActionKind kind, int target)
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
            };
        }

        [Fact]
        public static void Constructor_SetsUpUnitsAndInitialSnapshot()
        {
            Game game = NewDuel(Role.Sniper, Role.Heavy, 2, 4);
            Assert.Equal(2, game.State.Units[0].Health);
            Assert.Equal(4, game.State.Units[2].Health);
            Assert.Equal(1, game.State.Units[2].Armor);

            Assert.Equal(4, game.State.Units[2].Position);
            Assert.Equal(3, game.State.Units[1].Health);
            Assert.Equal(0, game.State.Units[4].Health);
            Assert.Single(game.Snapshots);
            Assert.Equal(1, game.State.RoundNumber);
            Assert.True(game.State.Possible[1][2][1]);
            Assert.False(game.State.Possible[1][2][0]);
        }

        [Fact]
        public static void Constructor_RejectsBadSetup()
        {
            Assert.Throws<ArgumentException>(() =>
                new Game(
                    new int[] { 2, 9, 4, 8, 0, 0, 0, 0 },
                    new Role[]
                    {
                        Role.Sniper,
                        Role.Sniper,
                        Role.Wolf,
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
                )
            );
            Assert.Throws<ArgumentException>(() => NewDuel(Role.Sniper, Role.Wolf, 5, 4));
            Assert.Throws<ArgumentException>(() =>
                new Game(new int[8], new Role[8], Mode.Heroes, 5, null, 2, 2, false)
            );
        }

        [Fact]
        public static void View_MasksEnemyPrivateFields()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            View mine = game.View(0);
            Assert.Equal(2, mine.Units[0].Health);
            Assert.Equal(0, mine.Units[2].Position);

            Assert.Equal(string.Empty, mine.Units[2].Report);
            Assert.Null(mine.Units[2].Follow);
            Assert.Equal(3, mine.Units[2].Health);

            View theirs = game.View(1);
            Assert.Null(theirs.Budget.Active);
            Assert.Equal(0, theirs.Budget.Moves);
        }

        [Fact]
        public void Act_Move_UpdatesStateAndAutoEnds()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            Assert.True(game.Act(Do(0, ActionKind.Move, 7)));
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.Equal(1, game.State.Revision);
            Assert.Equal(2, game.Snapshots.Count);
            Assert.Equal(1, game.State.Turn);
            Assert.Contains("Move to", game.Events[0].PrivateText);
            Assert.DoesNotContain('7', game.Events[0].PublicText);
        }

        [Fact]
        public static void Act_RejectsIllegalActions()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            Assert.False(game.Act(Do(0, ActionKind.Move, 4)));
            Assert.False(game.Act(Do(2, ActionKind.Move, 3)));
            Assert.False(game.Act(Do(0, ActionKind.Dash, 7)));
            Assert.Equal(0, game.State.Revision);
        }

        [Fact]
        public static void Attack_SniperExecute_KillsThroughArmor()
        {
            Game game = NewDuel(Role.Sniper, Role.Heavy, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(0, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Units[2].Armor);
            Assert.Null(game.State.Winner);
            Assert.False(game.Finished);
            Assert.Contains("Kill", game.Events[0].PublicText);
        }

        [Fact]
        public void Damage_PerObjectCap_OnlyPaysDifferenceWithinRound()
        {
            Game game = NewDuel(Role.Wolf, Role.Heavy, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(4, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Units[2].Armor);
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(4, game.State.Units[2].Health);
            Assert.Contains("no less than", game.Events[1].PublicText);
            Assert.Equal(1, game.State.Turn);
            Assert.True(game.Act(Do(2, ActionKind.End, 0)));
            Assert.Equal(2, game.State.RoundNumber);
            game.Act(Do(0, ActionKind.Attack, 7));
            Assert.Equal(2, game.State.Units[2].Health);
        }

        [Fact]
        public void Attack_FriendlyFire_Misses()
        {
            Game game = new Game(
                new int[] { 2, 7, 4, 9, 0, 0, 0, 0, 0 },
                new Role[]
                {
                    Role.Sniper,
                    Role.Wolf,
                    Role.Wolf,
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
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(3, game.State.Units[1].Health);
            Assert.Contains("Missed", game.Events[0].PublicText);
        }

        [Fact]
        public void Attack_WallBlocksLineOfFire()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 8, 18);
            Assert.False(game.Act(Do(0, ActionKind.Attack, 18)));
            Game game2 = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            Assert.True(game2.Act(Do(0, ActionKind.Attack, 4)));
            Assert.Equal(0, game2.State.Units[2].Health);
        }

        [Fact]
        public void ScoutMode_BasicMoveAndAttack()
        {
            var game = new Game(
                new int[] { 1, 0, 2, 0, 0, 0, 0, 0 },
                new Role[8],
                Mode.Scout,
                0,
                null,
                2,
                2,
                false
            );
            Assert.Equal(Role.Sniper, game.State.Units[0].Role);
            Assert.True(game.Act(Do(0, ActionKind.Attack, 2)));
            Assert.Equal(0, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Winner);
        }

        [Fact]
        public void FortyRounds_EndInDraw()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            for (int i = 0; i < 200 && !game.Finished; ++i)
            {
                int turn = game.State.Turn;
                Assert.True(game.Act(Do(turn * 2, ActionKind.End, 0)));
            }
            Assert.Equal(4, game.State.Winner);
            Assert.Equal(80, game.State.Turns);
        }

        [Fact]
        public void Surrender_EndsGameWithOpponentWin()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            Assert.True(game.Act(Do(0, ActionKind.Surrender, 0)));
            Assert.Equal(1, game.State.Winner);
            Assert.True(game.Finished);
        }

        [Fact]
        public void Snapshots_AreIndependentCopies()
        {
            Game game = NewDuel(Role.Wolf, Role.Wolf, 2, 4);
            game.Act(Do(0, ActionKind.Move, 7));
            game.State.Units[0].Position = 25;
            Assert.Equal(2, game.Snapshots[0].Units[0].Position);
            Assert.Equal(7, game.Snapshots[1].Units[0].Position);
        }
    }
}
