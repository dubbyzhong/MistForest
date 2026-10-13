using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    /// M2.8c: WallGuard - soldiers, squad sharing, great walls, whirlpools.
    public class GuardTests
    {
        private static readonly int[] positions = new int[] { 2, 9, 7, 4, 0, 0, 0, 0 };

        private static Game NewGuardDuel(int[]? formation, Map? map = null)
        {
            map ??= new Map();
            int[]?[] formations = new int[8][];
            formations[0] = formation;
            return new Game(
                positions,
                new Role[]
                {
                    Role.WallGuard,
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
                map,
                2,
                2,
                false,
                formations
            );
        }

        private static GameAction Do(
            int unit,
            ActionKind kind,
            int target,
            int piece = 0,
            int amount = 1
        )
        {
            return new GameAction
            {
                Unit = unit,
                Kind = kind,
                Target = target,
                Piece = piece,
                Amount = amount,
            };
        }

        [Fact]
        public static void Construction_SpawnFormationAndHealth()
        {
            Game game = NewGuardDuel(null);
            Assert.Equal(new int[] { 2, 1, 3 }, game.State.Units[0].Soldiers);
            Assert.Equal(4.8, game.State.Units[0].Health, 3);
            Assert.Equal(new double[] { 1.6, 1.6, 1.6 }, game.State.Units[0].SoldierHealth);
            Assert.Throws<ArgumentException>(() => NewGuardDuel(new int[] { 1, 2, 3 }));
            Assert.Throws<ArgumentException>(() => NewGuardDuel(new int[] { 2, 0, 3 }));
        }

        [Fact]
        public static void SquadShare_WhenItDoesNotIncreaseDeaths()
        {
            Game game = NewGuardDuel(new int[] { 2, 1, 20 });
            game.Act(Do(0, ActionKind.End, 0));
            Assert.True(game.Act(Do(2, ActionKind.Attack, 2)));
            Assert.Equal(0.6, game.State.Units[0].SoldierHealth[0], 3);
            Assert.Equal(0.6, game.State.Units[0].SoldierHealth[1], 3);
            Assert.Equal(1.6, game.State.Units[0].SoldierHealth[2], 3);
            Assert.Equal(2.8, game.State.Units[0].Health, 3);
            game.Act(Do(2, ActionKind.End, 0));
            game.Act(Do(0, ActionKind.End, 0));
            Assert.True(game.Act(Do(2, ActionKind.Attack, 2)));
            Assert.Equal(0, game.State.Units[0].SoldierHealth[0]);
            Assert.Equal(0.6, game.State.Units[0].SoldierHealth[1], 3);
            Assert.Equal(1.6, game.State.Units[0].SoldierHealth[2], 3);
        }

        [Fact]
        public static void SoldierActions_PerSoldierBudget_AutoEndAfterAllThree()
        {
            Game game = NewGuardDuel(new int[] { 2, 1, 6 });
            Assert.True(game.Act(Do(0, ActionKind.Move, 2, 0)));
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.Move, 1, 1)));
            Assert.Equal(0, game.State.Turn);
            Assert.True(game.Act(Do(0, ActionKind.Move, 6, 2)));
            Assert.Equal(1, game.State.Turn);
            Game game2 = NewGuardDuel(new int[] { 2, 1, 6 });
            game2.Act(Do(0, ActionKind.Move, 7, 0));
            Assert.False(game2.Act(Do(0, ActionKind.Move, 2, 0)));
            Assert.True(game2.Act(Do(0, ActionKind.Move, 1, 1)));
            Assert.Equal(0, game2.State.Turns);
        }

        [Fact]
        public static void SoldierAttack_DamagesEnemy()
        {
            Game game = NewGuardDuel(new int[] { 2, 1, 6 });
            game.State.Units[2].Position = 3;
            Assert.True(game.Act(Do(0, ActionKind.Attack, 3, 0)));
            Assert.Equal(1, game.State.Units[2].Health);
        }

        [Fact]
        public static void GuardWall_RangeTwo()
        {
            Game onWall = NewGuardDuel(new int[] { 2, 1, 6 });
            onWall.State.Buildings[1] = new Building
            {
                Kind = Structure.Wall,
                Owner = 1,
                Health = 2,
            };
            Assert.True(onWall.Act(Do(0, ActionKind.Attack, 12, 0)));
            Game offWall = NewGuardDuel(new int[] { 2, 1, 6 });
            Assert.False(offWall.Act(Do(0, ActionKind.Attack, 12, 0)));
        }

        [Fact]
        public static void GuardBuildsGreatWall_CannotBuildFort()
        {
            Game game = NewGuardDuel(new int[] { 2, 1, 6 });
            Assert.False(game.Act(Do(0, ActionKind.BuildFort, 3, 0)));
            Assert.True(game.Act(Do(0, ActionKind.BuildWall, 3, 0)));
            Assert.Equal(Structure.GreatWall, game.State.Buildings[2].Kind);
            Assert.Equal(0, game.State.Buildings[2].Owner);
            Assert.Equal(2, game.State.Buildings[2].Health);
        }

        [Fact]
        public static void GuardDemolishes_OwnBuildingOnly_HouseRule()
        {
            Game game = NewGuardDuel(new int[] { 2, 1, 6 });
            game.State.Buildings[6] = new Building
            {
                Kind = Structure.Fort,
                Owner = 1,
                Health = 2,
            };
            Assert.False(game.Act(Do(0, ActionKind.Demolish, 7, 0)));
            game.State.Buildings[6].Owner = 0;
            Assert.True(game.Act(Do(0, ActionKind.Demolish, 7, 0)));
            Assert.Equal(0, game.State.Buildings[6].Health);
            Assert.True(game.State.Rubble[6] != 0);
        }

        [Fact]
        public static void Whirlpool_PullsSoldiers()
        {
            var map = new Map();
            map.Cells[6] = Terrain.Whirlpool;
            Game game = NewGuardDuel(new int[] { 2, 1, 6 }, map);
            Assert.Equal(7, game.State.Units[0].Soldiers[0]);
            Assert.True(game.State.Units[0].SoldierWhirl[0]);
            Assert.True(game.State.Units[0].WhirlImmune);
            Assert.Equal(7, game.State.Units[0].Position);
        }
    }
}
