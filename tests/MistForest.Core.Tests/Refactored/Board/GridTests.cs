using MistForest.Core.GameMap;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Mapping
{
    public class GridTests
    {
        [Fact]
        public static void New_DefaultsToGround()
        {
            Assert.Equal(Terrain.Ground, Grid.New().Terrain);
        }

        [Fact]
        public static void WithOriginalBuilding_SetsAndReturnsSelf()
        {
            Grid grid = Grid.New();
            Assert.Same(grid, grid.WithOriginalBuilding(Terrain.Lake));
            Assert.Equal(Terrain.Lake, grid.Terrain);
        }

        [Fact]
        public static void Clone_IsIndependent()
        {
            Grid grid = Grid.New().WithOriginalBuilding(Terrain.Wall);
            Grid clone = grid.Clone();
            clone.Terrain = Terrain.Lake;
            Assert.NotSame(grid, clone);
            Assert.Equal(Terrain.Wall, grid.Terrain);
            Assert.Equal(Terrain.Lake, clone.Terrain);
        }
    }
}
