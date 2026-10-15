using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class ShadowTests
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
            int subject = -1,
            int amount = 1
        )
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Subject = subject,
                Amount = amount,
            };
        }

        [Fact]
        public static void ShadowMode_RequiredFirst_TwoModes()
        {
            Game game = NewDuel(Role.Shadow, Role.Wolf, 2, 22);
            Assert.False(game.Act(Do(0, ActionKind.Move, 7)));
            Assert.True(game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2)));
            Assert.Equal(2, game.State.Budget.ShadowMode);
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.Move, 7)));

            Game game2 = NewDuel(Role.Shadow, Role.Wolf, 2, 22);
            game2.Act(Do(0, ActionKind.ShadowMode, 0, amount: 1));
            Assert.False(game2.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2)));
        }

        [Fact]
        public static void Wrap_MoveAcrossEdges()
        {
            Game game = NewDuel(Role.Shadow, Role.Wolf, 1, 22);
            game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2));
            Assert.True(game.Act(Do(0, ActionKind.Move, 5)));
            Assert.Equal(5, game.State.Units[0].Position);

            Game game2 = NewDuel(Role.Shadow, Role.Wolf, 5, 22);
            game2.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2));
            Assert.True(game2.Act(Do(0, ActionKind.Move, 1)));
            Assert.Equal(1, game2.State.Units[0].Position);
        }

        [Fact]
        public static void Wrap_AttackAcrossEdge()
        {
            Game game = NewDuel(Role.Shadow, Role.Wolf, 1, 5);
            game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2));
            Assert.True(game.Act(Do(0, ActionKind.Attack, 5)));
            Assert.Equal(1, game.State.Units[2].Health);
        }

        [Fact]
        public static void Follow_SyncsOnEnemyTurn_ClearedAfterTargetOwnerTurn()
        {
            Game game = new Game(
                new int[] { 2, 9, 7, 4, 0, 0, 0, 0 },
                new Role[]
                {
                    Role.Shadow,
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
                new Map(),
                2,
                2,
                false
            );
            Assert.True(game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2)));
            Assert.True(game.Act(Do(0, ActionKind.Follow, 0, subject: 2)));
            Assert.Equal(2, game.State.Units[0].Follow);
            Assert.False(game.Act(Do(1, ActionKind.Move, 8)));
            Assert.True(game.Act(Do(0, ActionKind.End, 0)));
            Assert.True(game.Act(Do(2, ActionKind.Move, 8)));
            Assert.Equal(3, game.State.Units[0].Position);
            Assert.Contains("Attachment synced", game.State.Units[0].Report);
            game.Act(new GameAction { Unit = 2, Kind = ActionKind.End });
            Assert.Null(game.State.Units[0].Follow);
        }

        [Fact]
        public static void Lead_DragsTargetAlong()
        {
            Game game = new Game(
                new int[] { 2, 9, 22, 4, 0, 0, 0, 0 },
                new Role[]
                {
                    Role.Shadow,
                    Role.Wolf,
                    Role.Sniper,
                    Role.Wolf,
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
            Assert.True(game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 1)));
            Assert.True(game.Act(Do(0, ActionKind.Lead, 7, subject: 1)));
            Assert.Equal(7, game.State.Units[0].Position);
            Assert.Equal(14, game.State.Units[1].Position);
            Assert.Contains("Lead feedback", game.State.Units[0].Report);
        }

        [Fact]
        public static void ShadowAttackWrap_FarTargetRejected()
        {
            Game game = NewDuel(Role.Shadow, Role.Wolf, 2, 23);
            game.Act(Do(0, ActionKind.ShadowMode, 0, amount: 2));
            Assert.False(game.Act(Do(0, ActionKind.Attack, 23)));
        }
    }
}
