using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    public class Grid : IClone<Grid>
    {
        public Terrain OriginalBuilding;

        public static Grid New()
        {
            return new() { OriginalBuilding = Terrain.Ground };
        }

        public Grid WithOriginalBuilding(Terrain building)
        {
            OriginalBuilding = building;
            return this;
        }

        public Grid Clone()
        {
            return new Grid { OriginalBuilding = OriginalBuilding };
        }

        public bool CanInitialStay()
        {
            return OriginalBuilding is Terrain.Ground or Terrain.Whirlpool;
        }
    }
}
