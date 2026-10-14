using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class RoleActionTests
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

        private static void EndCurrent(Game game)
        {
            game.Act(new GameAction { Unit = game.State.Turn * 2, Kind = ActionKind.End });
        }

        [Fact]
        public static void Transform_OncePerTurn_HumanGainsShield()
        {
            Game game = NewDuel(Role.HiddenWolf, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.Transform, 1)));
            Assert.Equal(1, game.State.Units[0].Form);
            Assert.True(game.State.Budget.Transformed);
            Assert.False(game.Act(Do(0, ActionKind.Transform, 0)));
            EndCurrent(game);
            EndCurrent(game);
            Assert.True(game.Act(Do(0, ActionKind.Transform, 0)));
            Assert.Equal(1, game.State.Units[0].Shield, 3);
            EndCurrent(game);
            EndCurrent(game);
            Assert.Equal(0, game.State.Units[0].Shield);
        }

        [Fact]
        public static void Dash_TwoStepPath_CooldownAndRefresh()
        {
            Game game = NewDuel(Role.Ink, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.Dash, 8)));
            Assert.Equal(8, game.State.Units[0].Position);
            Assert.Equal(3, game.State.Units[0].Cooldown);
            Assert.False(game.Act(Do(0, ActionKind.Dash, 2)));
            game.State.Units[2].Position = 7;
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(2, game.State.Units[2].Health);
            Assert.Equal(0, game.State.Units[0].Cooldown);
            Assert.True(game.Act(Do(0, ActionKind.Dash, 2)));
        }

        [Fact]
        public static void Charge_FourSteps_DamageFromStepTwo()
        {
            Game game = NewDuel(Role.Heavy, Role.Wolf, 2, 22);
            game.State.Units[2].Position = 17;
            Assert.True(game.Act(Do(0, ActionKind.Charge, 7)));
            Assert.Equal(3, game.State.Units[2].Health);
            Assert.True(game.Act(Do(0, ActionKind.Charge, 12)));
            Assert.True(game.Act(Do(0, ActionKind.Charge, 17)));
            Assert.Equal(1, game.State.Units[2].Health);
            Assert.True(game.Act(Do(0, ActionKind.Charge, 22)));
            Assert.Equal(1, game.State.Turn);
            Assert.True(game.Events[3].AttackCells[11]);
            Assert.True(game.Events[3].AttackCells[16]);
            Assert.True(game.Events[3].AttackCells[21]);
        }

        [Fact]
        public static void Charge_DirectionRules()
        {
            Game game = NewDuel(Role.Heavy, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.Charge, 7)));
            Assert.True(game.Act(Do(0, ActionKind.Charge, 8)));
            Assert.False(game.Act(Do(0, ActionKind.Charge, 13)));
            Assert.True(game.Act(Do(0, ActionKind.Charge, 9)));

            Game game2 = NewDuel(Role.Heavy, Role.Wolf, 2, 22);
            Assert.True(game2.Act(Do(0, ActionKind.Charge, 7)));
            Assert.False(game2.Act(Do(0, ActionKind.Charge, 2)));

            Game game3 = NewDuel(Role.Heavy, Role.Wolf, 2, 22);
            Assert.True(game3.Act(Do(0, ActionKind.Move, 7)));
            Assert.False(game3.Act(Do(0, ActionKind.Charge, 12)));
        }

        [Fact]
        public static void Charge_SmashesBuildingFromStepTwo()
        {
            Game game = NewDuel(Role.Heavy, Role.Wolf, 2, 22);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Wall,
                Owner = 1,
                Health = 2,
            };
            Assert.False(game.Act(Do(0, ActionKind.Charge, 7)));
            game.State.Budget = new Turn();
            game.State.Buildings[12] = new Building
            {
                Kind = Structure.Wall,
                Owner = 1,
                Health = 2,
            };
            Assert.True(game.Act(Do(0, ActionKind.Charge, 3)));
            Assert.True(game.Act(Do(0, ActionKind.Charge, 8)));
            Assert.True(game.Act(Do(0, ActionKind.Charge, 13)));
            Assert.False(game.Act(Do(0, ActionKind.Charge, 18)));
            Assert.Equal(0, game.State.Buildings[12].Health);
            Assert.True(game.State.Rubble[12] != 0);
            Assert.Equal(13, game.State.Units[0].Position);
        }

        [Fact]
        public static void Armiger_ArmorUpDownThrust()
        {
            Game game = NewDuel(Role.Armiger, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.ArmorUp, 0)));
            Assert.Equal(1, game.State.Units[0].Armor);
            Assert.Equal(1, game.State.Turn);
            EndCurrent(game);
            Assert.True(game.Act(Do(0, ActionKind.Move, 3)));
            Assert.False(game.Act(Do(0, ActionKind.Move, 4)));
            Assert.True(game.Act(Do(0, ActionKind.ArmorDown, 0, 1)));
            Assert.Equal(0, game.State.Units[0].Armor);
            EndCurrent(game);
            EndCurrent(game);
            game.State.Units[2].Position = 4;
            game.State.Units[0].Position = 2;
            Assert.True(game.Act(Do(0, ActionKind.ArmorUp, 0)));
            EndCurrent(game);
            Assert.True(game.Act(Do(0, ActionKind.Thrust, 3)));
            Assert.Equal(1, game.State.Units[2].Health);
        }

        [Fact]
        public static void Armiger_ArmorLockAfterHit()
        {
            Game game = NewDuel(Role.Wolf, Role.Armiger, 22, 2);
            game.State.Units[0].Position = 7;
            game.Act(new GameAction { Unit = 0, Kind = ActionKind.End });
            Assert.True(game.Act(Do(2, ActionKind.ArmorUp, 0)));
            Assert.True(game.Act(Do(0, ActionKind.Attack, 2)));
            Assert.Equal(2, game.State.Units[2].ArmorLock);
            EndCurrent(game);
            Assert.False(game.Act(Do(2, ActionKind.ArmorUp, 0)));
        }
    }
}
