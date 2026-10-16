namespace MistForest.Core.GameMap
{
    public class Grid
    {
        public Terrain OriginalBuilding;

        private Grid()
        {
            OriginalBuilding = Terrain.Ground;
        }

        public static Grid New() => new();

        public Grid WithOriginalBuilding(Terrain building)
        {
            OriginalBuilding = building;
            return this;
        }
    }
}
