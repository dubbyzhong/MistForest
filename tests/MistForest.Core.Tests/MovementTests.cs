using Map = MistForest.Core.Model.Map;
using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class MovementTests
    {
        [Fact]
        public void CanMove_AdjacentOnEmptyMap()
        {
            var map = new Map();
            Assert.True(Movement.CanMove(Mode.Heroes, 1, 2, map));
            Assert.True(Movement.CanMove(Mode.Heroes, 1, 6, map));
            Assert.True(Movement.CanMove(Mode.Heroes, 7, 2, map));
            Assert.True(Movement.CanMove(Mode.Heroes, 4, 3, map));
        }

        [Fact]
        public void CanMove_DistanceAndTerrainRules()
        {
            var map = new Map();
            Assert.False(Movement.CanMove(Mode.Heroes, 1, 3, map));
            Assert.False(Movement.CanMove(Mode.Heroes, 1, 7, map));
            Assert.True(Movement.CanMove(Mode.Heroes, 4, 4, map));
            map.Cells[1] = Terrain.Lake;
            Assert.False(Movement.CanMove(Mode.Heroes, 1, 2, map));
            map.Cells[1] = Terrain.Whirlpool;
            Assert.True(Movement.CanMove(Mode.Heroes, 1, 2, map));
        }

        [Fact]
        public void CanMove_LineWallsBlock()
        {
            var map = new Map();
            map.VerticalWalls[0] = true;
            Assert.False(Movement.CanMove(Mode.Heroes, 1, 2, map));
            Assert.False(Movement.CanMove(Mode.Heroes, 2, 1, map));
            map.HorizontalWalls[0] = true;
            map.VerticalWalls[0] = false;
            Assert.False(Movement.CanMove(Mode.Heroes, 1, 6, map));
            Assert.True(Movement.CanMove(Mode.Heroes, 2, 1, map));
        }

        [Fact]
        public void ClearShot_BoxWallBlocksButGrazingPasses()
        {
            var map = new Map();
            Assert.True(Movement.ClearShot(Mode.Heroes, 1, 25, map));

            map.Cells[12] = Terrain.Wall;
            Assert.False(Movement.ClearShot(Mode.Heroes, 1, 25, map));
            Assert.False(Movement.ClearShot(Mode.Heroes, 8, 18, map));
            Assert.True(Movement.ClearShot(Mode.Heroes, 5, 1, map));
            Assert.False(Movement.ClearShot(Mode.Heroes, 14, 1, map));
            Assert.True(Movement.ClearShot(Mode.Heroes, 2, 14, map));

            var graze = new Map();
            graze.Cells[1] = Terrain.Wall;
            Assert.True(Movement.ClearShot(Mode.Heroes, 1, 7, graze));
            graze.Cells[5] = Terrain.Wall;
            Assert.False(Movement.ClearShot(Mode.Heroes, 1, 7, graze));

            var lake = new Map();
            lake.Cells[5] = Terrain.Lake;
            Assert.True(Movement.ClearShot(Mode.Heroes, 1, 11, lake));
        }

        [Fact]
        public void ClearShot_LineWallBlocksStraightShot()
        {
            var map = new Map();
            map.VerticalWalls[0] = true;
            Assert.False(Movement.ClearShot(Mode.Heroes, 1, 2, map));
            Assert.True(Movement.ClearShot(Mode.Heroes, 6, 1, map));
        }

        [Fact]
        public void ClearShot_LineWallBlocksCenterShot()
        {
            var map = new Map();
            map.HorizontalWalls[0] = true;
            map.HorizontalWalls[1] = true;
            Assert.False(Movement.ClearShot(Mode.Heroes, 1, 7, map));
        }

        [Fact]
        public void ClearShot_LShapeLineWallShot()
        {
            var map = new Map();
            map.VerticalWalls[0] = true;
            map.HorizontalWalls[1] = true;
            Assert.True(Movement.ClearShot(Mode.Heroes, 1, 7, map));
            Assert.False(Movement.ClearShot(Mode.Heroes, 2, 6, map));
        }

        [Fact]
        public void ClearShot_LShapBoxAndLineWallShot()
        {
            var map = new Map();
            map.Cells[1] = Terrain.Wall;
            map.HorizontalWalls[0] = true;
            Assert.False(Movement.ClearShot(Mode.Heroes, 1, 7, map));
            map.HorizontalWalls[0] = false;
            map.HorizontalWalls[1] = true;
            Assert.True(Movement.ClearShot(Mode.Heroes, 1, 7, map));
        }

        [Fact]
        public void DashPath_TwoStepPath_VerticalFirst()
        {
            var map = new Map();
            List<int> path = Movement.DashPath(Mode.Heroes, 1, 7, map);
            Assert.Equal(new List<int> { 6, 7 }, path);
            Assert.Equal(new List<int> { 2, 3 }, Movement.DashPath(Mode.Heroes, 1, 3, map));
            Assert.Empty(Movement.DashPath(Mode.Heroes, 1, 10, map));
            Assert.Empty(Movement.DashPath(Mode.Heroes, 1, 2, map));

            var blocked = new Map();
            blocked.HorizontalWalls[0] = true;
            blocked.VerticalWalls[0] = true;
            Assert.Empty(Movement.DashPath(Mode.Heroes, 1, 7, blocked));
        }

        [Fact]
        public void CanLeap_KnightJumpRules()
        {
            var map = new Map();
            Assert.True(Movement.CanLeap(Mode.Heroes, 8, 19, map));
            Assert.False(Movement.CanLeap(Mode.Heroes, 8, 20, map));
            Assert.True(Movement.CanLeap(Mode.Heroes, 8, 17, map));
            Assert.False(Movement.CanLeap(Mode.Heroes, 8, 8, map));

            map.Cells[12] = Terrain.Wall;
            Assert.False(Movement.CanLeap(Mode.Heroes, 8, 19, map));
            map.Cells[12] = Terrain.Lake;
            Assert.True(Movement.CanLeap(Mode.Heroes, 8, 17, map));
            map.HorizontalWalls[1 * 5 + 2] = true;
            Assert.False(Movement.CanLeap(Mode.Heroes, 8, 19, map));
        }

        [Theory]
        [InlineData(Mode.Heroes, 1, 1, true)]
        [InlineData(Mode.Heroes, 1, 2, true)]
        [InlineData(Mode.Heroes, 1, 7, false)]
        [InlineData(Mode.Heroes, 1, 0, false)]
        [InlineData(Mode.Scout, 1, 4, true)]
        public void SilentArea_OnlyCrossWithinOne(Mode mode, int origin, int target, bool expected)
        {
            Assert.Equal(expected, GameRules.SilentArea(mode, origin, target, new Map()));
        }

        [Fact]
        public void Immune_OnlyHumanHiddenWolf()
        {
            var human = new Unit { Role = Role.HiddenWolf, Form = 0 };
            var wolf = new Unit { Role = Role.HiddenWolf, Form = 1 };
            var other = new Unit { Role = Role.Wolf };
            Assert.True(GameRules.Immune(human));
            Assert.False(GameRules.Immune(wolf));
            Assert.False(GameRules.Immune(other));
        }

        [Theory]
        [InlineData(Mode.Scout, Role.Wolf, 1)]
        [InlineData(Mode.Heroes, Role.Sniper, 3)]
        [InlineData(Mode.Heroes, Role.HiddenWolf, 2)]
        [InlineData(Mode.Heroes, Role.Wolf, 1)]
        [InlineData(Mode.Heroes, Role.Sword, 1)]
        public void AttackRange_ByRoleAndMode(Mode mode, Role role, int expected)
        {
            var unit = new Unit { Role = role, Form = 0 };
            Assert.Equal(expected, GameRules.AttackRange(mode, unit));
            Assert.Equal(
                1,
                GameRules.AttackRange(mode, new Unit { Role = Role.HiddenWolf, Form = 1 })
            );
        }
    }
}
