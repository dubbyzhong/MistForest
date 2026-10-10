namespace MistForest.Core.Model
{
    /// <summary>
    /// The rules and constraints of map.
    /// </summary>
    public static class MapRules
    {
        /// <summary>
        /// Judge whether a map mode is valid.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static bool ValidMode(Mode mode)
        {
            if (mode == Mode.Scout || mode == Mode.Heroes)
            {
                return true;
            }
            int selfDefinedMode = (int)mode;
            if (!(selfDefinedMode >= 11 && selfDefinedMode <= 99))
            {
                return false;
            }
            return (selfDefinedMode / 10) >= 1 && (selfDefinedMode % 10) >= 1;
        }

        /// <summary>
        /// Generate a default map.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static Map DefaultMap(Mode mode)
        {
            var map = new Map();
            map.Height = MapInfo.Rows(mode);
            map.Width = MapInfo.Cols(mode);
            if (mode == Mode.Heroes)
            {
                (int X, int Y)[] walls = new[] { (1, 1), (1, 5), (3, 3), (5, 1), (5, 5) };
                foreach ((int X, int Y) wall in walls)
                {
                    int pos = (wall.X - 1) * map.Width + wall.Y;
                    map.Cells[pos - 1] = Terrain.Wall;
                }
            }
            return map;
        }

        /// <summary>
        /// Judge whether a map is valid.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool ValidMap(Mode mode, Map map)
        {
            if (
                !ValidMode(mode)
                || !(1 <= map.Width && map.Width <= 9)
                || !(1 <= map.Height && map.Height <= 9)
            )
            {
                return false;
            }
            int total = map.Width * map.Height;
            int standable = 0;
            for (int i = 0; i < 81; ++i)
            {
                var value = map.Cells[i];
                if ((int)value < 0 || (int)value > 3)
                {
                    return false;
                }
                if (i < total && (value == Terrain.Ground || value == Terrain.Whirlpool))
                {
                    ++standable;
                }
                if (i >= total && (int)value != 0)
                {
                    return false;
                }
            }
            for (int i = 0; i < 72; ++i)
            {
                if (i >= (map.Height - 1) * map.Width && map.HorizontalWalls[i])
                {
                    return false;
                }
                if (i >= map.Height * (map.Width - 1) && map.VerticalWalls[i])
                {
                    return false;
                }
            }
            return standable > 0;
        }

        /// <summary>
        /// Judge whether a cell is valid.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="cell"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool Valid(Mode mode, int cell, Map map)
        {
            if (!ValidMode(mode) || !(1 <= cell && cell <= MapInfo.CellCount(mode)))
            {
                return false;
            }
            return map.Cells[cell - 1] == Terrain.Ground
                || map.Cells[cell - 1] == Terrain.Whirlpool;
        }

        /// <summary>
        /// Judge whether a cell is valid. (override the default map)
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        public static bool Valid(Mode mode, int cell)
        {
            return Valid(mode, cell, DefaultMap(mode));
        }

        /// <summary>
        /// Calculate the Manhattan distance between two cells.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static int Distance(Mode mode, int a, int b)
        {
            if (a < 1 || b < 1)
            {
                throw new System.ArgumentException("a or b is out of range.");
            }
            int n = MapInfo.Cols(mode); // 跨度 = 每行格数 = 列数(个位)。与几何代码的 map.Width 一致。
            int cellA = a - 1;
            int cellB = b - 1;
            return System.Math.Abs(cellA / n - cellB / n) + System.Math.Abs(cellA % n - cellB % n);
        }
    }
}
