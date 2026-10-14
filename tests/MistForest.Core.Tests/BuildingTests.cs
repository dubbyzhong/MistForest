using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class BuildingTests
    {
        private static Game NewDuel(Role first, Role second, int posFirst, int posSecond)
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
                null,
                2,
                2,
                false
            );
        }

        private static GameAction Do(int unit, ActionKind kind, int target, int extra = 0)
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Extra = extra,
            };
        }

        [Fact]
        public static void BuildFort_EndToEnd_ConsumesWholeTurn()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 7);
            Assert.True(game.Act(Do(0, ActionKind.BuildFort, 3)));
            Assert.Equal(Structure.Fort, game.State.Buildings[2].Kind);
            Assert.Equal(0, game.State.Buildings[2].Owner);
            Assert.Equal(2, game.State.Buildings[2].Health);
            Assert.Equal(0, game.State.Rubble[2]);
            Assert.Equal(1, game.State.Turn);
            Assert.Contains("Build succeeded", game.Events[0].PublicText);
        }

        [Fact]
        public static void BuildWall_CreatesTwoSegments()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 22);
            Assert.True(game.Act(Do(0, ActionKind.BuildWall, 7, 8)));
            Assert.Equal(Structure.Wall, game.State.Buildings[6].Kind);
            Assert.Equal(Structure.Wall, game.State.Buildings[7].Kind);
        }

        [Fact]
        public static void BuildFails_WhenHiddenEnemyStandsOnTarget()
        {
            Game game = new Game(
                new int[] { 2, 9, 3, 4, 0, 0, 0, 0 },
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
            Assert.True(game.Act(Do(0, ActionKind.BuildFort, 3)));
            Assert.Equal(0, game.State.Buildings[2].Health);
            Assert.Equal(1, game.State.Turn);
            Assert.Contains("Build failed", game.Events[0].PublicText);
            Assert.DoesNotContain(
                "enemy",
                game.Events[0].PublicText,
                StringComparison.OrdinalIgnoreCase
            );
        }

        [Fact]
        public static void Build_RejectedAfterFirstAction()
        {
            Game game = NewDuel(Role.Heavy, Role.Wolf, 2, 7);
            game.Act(Do(0, ActionKind.End, 0));
            game.Act(Do(2, ActionKind.End, 0));
            game.Act(Do(0, ActionKind.Move, 7));
            Assert.False(game.Act(Do(0, ActionKind.BuildFort, 3)));
            Assert.Equal(2, game.State.Turns);
        }

        [Fact]
        public static void Demolish_LeavesRubble_WhichClearsAfterTwoTurns()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 7);
            game.Act(Do(0, ActionKind.BuildFort, 3));
            game.Act(Do(2, ActionKind.End, 0));
            Assert.True(game.Act(Do(0, ActionKind.Demolish, 3)));
            Assert.Equal(0, game.State.Buildings[2].Health);
            Assert.True(game.State.Rubble[2] != 0);
            game.Act(Do(2, ActionKind.End, 0));
            Assert.Equal(0, game.State.Rubble[2]);
        }

        [Fact]
        public static void DemolishOwnFortFromInside_Allowed()
        {
            Game game = NewDuel(Role.Firefly, Role.Wolf, 2, 7);
            game.State.Buildings[1] = new Building
            {
                Kind = Structure.Fort,
                Owner = 0,
                Health = 2,
            };
            Assert.True(game.Act(Do(0, ActionKind.Demolish, 2)));
            Assert.Equal(0, game.State.Buildings[1].Health);
            Assert.True(game.State.Rubble[1] != 0);
        }

        [Fact]
        public static void DemolishEnemyFort_Rejected()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 7);
            game.State.Buildings[2] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 2,
            };
            Assert.False(game.Act(Do(0, ActionKind.Demolish, 3)));
            Assert.Equal(2, game.State.Buildings[2].Health);
            Assert.Equal(0, game.State.Rubble[2]);
            Assert.Equal(0, game.State.Turns);
        }

        [Fact]
        public static void EnemyFort_BlocksMove_ButOwnFortIsPassable()
        {
            Game game = NewDuel(Role.Wolf, Role.Sniper, 2, 7);
            game.State.Buildings[2] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 2,
            };
            Assert.False(game.Act(Do(0, ActionKind.Move, 3)));

            Game game2 = NewDuel(Role.Wolf, Role.Sniper, 2, 7);
            game2.State.Buildings[2] = new Building
            {
                Kind = Structure.Fort,
                Owner = 0,
                Health = 2,
            };
            Assert.True(game2.Act(Do(0, ActionKind.Move, 3)));
            Assert.Equal(3, game2.State.Units[0].Position);
        }

        [Fact]
        public static void Buildings_BlockLineOfFire_ExceptTargetItself()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            game.State.Buildings[2] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 4,
            };
            Assert.False(game.Act(Do(0, ActionKind.Attack, 4)));

            Game game2 = NewDuel(Role.Sniper, Role.Wolf, 2, 4);
            game2.State.Buildings[2] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 4,
            };
            game2.Act(Do(0, ActionKind.Attack, 3));
            Assert.Equal(0, game2.State.Buildings[2].Health);
            Assert.NotEqual(0, game2.State.Rubble[2]);
            Assert.Contains("destroyed the building", game2.Events[0].PublicText);
        }

        [Fact]
        public static void Building_ProtectsUnitInside_EvenWhenDestroyed()
        {
            Game game = NewDuel(Role.HiddenWolf, Role.Wolf, 2, 7);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 2,
            };
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(0, game.State.Buildings[6].Health);
            Assert.Equal(3, game.State.Units[2].Health);
            Assert.NotEqual(0, game.State.Rubble[6]);
        }

        [Fact]
        public static void BuildingDamage_PerObjectCap_AndInkDouble()
        {
            Game game = NewDuel(Role.Ink, Role.Wolf, 2, 7);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 4,
            };
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(2, game.State.Buildings[6].Health);
            Assert.Equal(2, game.State.Buildings[6].DirectDamage);
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(2, game.State.Buildings[6].Health);
            Assert.Contains("no stacking", game.Events[1].PublicText);
        }

        [Fact]
        public static void Wolf_CannotDamageBuildings_FriendlyFireImmune()
        {
            Game game = NewDuel(Role.Wolf, Role.Sniper, 2, 7);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 2,
            };
            game.Act(Do(0, ActionKind.Attack, 7));
            Assert.Equal(2, game.State.Buildings[6].Health);
            Assert.Contains("wolf", game.Events[0].PublicText);

            Game game2 = NewDuel(Role.Wolf, Role.Sniper, 2, 7);
            game2.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 0,
                Health = 2,
            };
            game2.Act(Do(0, ActionKind.Attack, 7));
            Assert.Equal(2, game2.State.Buildings[6].Health);
            Assert.Contains("immune", game2.Events[0].PublicText);
        }

        [Fact]
        public static void RoundDamage_PublicLedgerRecordedPerCell()
        {
            Game game = NewDuel(Role.Wolf, Role.Heavy, 2, 7);
            game.Act(Do(0, ActionKind.Attack, 7));
            Assert.Equal(2, game.State.RoundDamage[6]);
            View view = game.View(1);
            Assert.Equal(2, view.RoundDamage[6]);
        }

        [Fact]
        public static void OwnBuilding_ImmuneEvenToSniperExecute()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 7);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 0,
                Health = 4,
            };
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(4, game.State.Buildings[6].Health);
            Assert.Equal(0, game.State.Rubble[6]);
            Assert.Contains("immune", game.Events[0].PublicText);
        }

        [Fact]
        public static void SniperExecute_PiercesBuildingProtection()
        {
            Game game = NewDuel(Role.Sniper, Role.Wolf, 2, 7);
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 4,
            };
            Assert.True(game.Act(Do(0, ActionKind.Attack, 7)));
            Assert.Equal(0, game.State.Buildings[6].Health);
            Assert.Equal(0, game.State.Units[2].Health);
        }
    }
}
