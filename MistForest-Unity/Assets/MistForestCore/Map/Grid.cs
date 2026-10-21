using System.Collections.Generic;
using MistForest.Core.GameRefactored;
using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    public class Grid : IClone<Grid>
    {
        public Terrain Terrain;
        public List<int> Units;
        public int? GridBuilding;

        private Grid(List<int> units)
        {
            Units = units;
        }

        public static Grid New()
        {
            return new(units: new List<int>()) { Terrain = Terrain.Ground, GridBuilding = null };
        }

        public Grid WithOriginalBuilding(Terrain building)
        {
            Terrain = building;
            return this;
        }

        public Grid Clone()
        {
            return new(units: new List<int>(Units))
            {
                Terrain = Terrain,
                GridBuilding = GridBuilding,
            };
        }

        public bool CanInitialStay()
        {
            return Terrain is Terrain.Ground or Terrain.Whirlpool;
        }

        public bool CanStay(int playerId, Game game)
        {
            return Terrain is Terrain.Ground or Terrain.Whirlpool
                && (
                    GridBuilding is not int buildingId
                    || game.Buildings[buildingId] is not Building.Wall
                        && game.Buildings[buildingId].Owner is int owner
                        && (
                            game.Buildings[buildingId] is Building.Rubble
                            || game.IsPlayerAllied(owner, playerId)
                        )
                );
        }

        public bool CanBuild()
        {
            return Terrain is Terrain.Ground or Terrain.Whirlpool && GridBuilding is null;
        }

        public bool CanShot(Game game)
        {
            return Terrain is not Terrain.Wall
                && (
                    GridBuilding is not int buildingId
                    || game.Buildings[buildingId] is Building.Rubble
                );
        }
    }
}
