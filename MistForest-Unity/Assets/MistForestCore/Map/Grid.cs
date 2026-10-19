using System.Collections.Generic;
using MistForest.Core.GameRefactored;
using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    public class Grid : IClone<Grid>
    {
        public Terrain Terrain;
        public List<int> Units;

        private Grid(List<int> units) => Units = units;

        public static Grid New()
        {
            return new(units: new List<int>()) { Terrain = Terrain.Ground };
        }

        public Grid WithOriginalBuilding(Terrain building)
        {
            Terrain = building;
            return this;
        }

        public Grid Clone()
        {
            return new(units: new List<int>(Units)) { Terrain = Terrain };
        }

        public bool CanInitialStay()
        {
            return Terrain is Terrain.Ground or Terrain.Whirlpool;
        }

        public bool CanStay(int playerId, Game game)
        {
            return Terrain is Terrain.Ground or Terrain.Whirlpool;
        }

        public bool CanShot()
        {
            return Terrain is not Terrain.Wall;
        }
    }
}
