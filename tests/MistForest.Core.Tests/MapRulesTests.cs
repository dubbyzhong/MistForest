using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Model.Tests
{
    public class MapRulesTests
    {
        [Fact]
        public void DefaultHeroesMap_CrossWallsOnly()
        {
            Map map = MapRules.DefaultMap(Mode.Heroes);
            Assert.Equal(5, map.Width);
            Assert.Equal(5, map.Height);
            foreach (int cell in new int[] { 1, 5, 13, 21, 25 })
            {
                Assert.Equal(Terrain.Wall, map.Cells[cell - 1]);
            }
            int walls = 0;
            foreach (Terrain t in map.Cells)
            {
                if (t == Terrain.Wall)
                {
                    walls++;
                }
            }
            Assert.Equal(5, walls);
        }

        [Theory]
        [InlineData(Mode.Heroes, 1, false)]
        [InlineData(Mode.Heroes, 4, true)]
        [InlineData(Mode.Heroes, 24, true)]
        [InlineData(Mode.Heroes, 26, false)]
        [InlineData(Mode.Heroes, 0, false)]
        [InlineData(Mode.Scout, 10, false)]
        [InlineData((Mode)34, 10, true)]
        [InlineData((Mode)34, 43, false)]
        public void Valid_onDefaultMap_ChecksWallsAndBounds(Mode mode, int cell, bool expected)
        {
            Assert.Equal(expected, MapRules.Valid(mode, cell));
        }

        [Theory]
        [InlineData(Mode.Heroes, 1, 1, 0)]
        [InlineData(Mode.Heroes, 1, 7, 2)]
        [InlineData(Mode.Heroes, 1, 25, 8)]
        [InlineData(Mode.Scout, 3, 7, 4)]
        [InlineData((Mode)34, 2, 12, 4)]
        public void Distance_IsManhattan(Mode mode, int a, int b, int expected)
        {
            Assert.Equal(expected, MapRules.Distance(mode, a, b));
        }

        [Fact]
        public void ValidMap_RejectsTerrainBeyondUsedArea()
        {
            Map map = MapRules.DefaultMap(Mode.Heroes);
            map.Cells[30] = Terrain.Lake;
            Assert.False(MapRules.ValidMap(Mode.Heroes, map));
        }

        [Fact]
        public void ValidMap_AcceptsDefaultAndCustomSizes()
        {
            Assert.True(MapRules.ValidMap(Mode.Heroes, MapRules.DefaultMap(Mode.Heroes)));
            Assert.True(MapRules.ValidMap(Mode.Scout, MapRules.DefaultMap(Mode.Scout)));
            Map custom = new Map();
            custom.Width = 7;
            custom.Height = 6;
            Assert.True(MapRules.ValidMap((Mode)76, custom));
        }
    }
}
