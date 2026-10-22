using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;

namespace MistForest.Core.Model
{
    public static class World
    {
        /// <summary>
        /// Judge whether a building is a fort-like structure.
        /// </summary>
        /// <param name="building"></param>
        /// <returns></returns>
        public static bool FortLike(Building building)
        {
            return building.Kind is Structure.Fort or Structure.GreatWall;
        }

        /// <summary>
        /// Judge whether a unit can enter a cell considering structure.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="id"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        public static bool CanEnter(View view, int id, int cell)
        {
            if (!MapRules.Valid(view.Mode, cell, view.Map))
            {
                return false;
            }
            Building building = view.Buildings[cell - 1];
            return building.Health <= 0 || FortLike(building) && building.Owner == id / 2;
        }

        /// <summary>
        /// Judge whether a unit can move to a cell considering structure.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="id"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool WorldMove(View view, int id, int from, int to)
        {
            return Movement.CanMove(view.Mode, from, to, view.Map) && CanEnter(view, id, to);
        }

        /// <summary>
        /// Judge whether a unit can shot to a cell considering structure.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool WorldShot(View view, int from, int to)
        {
            var map = Deep.CloneMap(view.Map);
            for (int cell = 1; cell <= 81; ++cell)
            {
                if (view.Buildings[cell - 1].Health > 0 && cell != from && cell != to)
                {
                    map.Cells[cell - 1] = Terrain.Wall;
                }
            }
            return Movement.ClearShot(view.Mode, from, to, map);
        }

        /// <summary>
        /// Shift a cell to a new position considering map boundary.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="from"></param>
        /// <param name="dRow"></param>
        /// <param name="dCol"></param>
        /// <returns></returns>
        public static int Shifted(Map map, int from, int dRow, int dCol)
        {
            int width = map.Width,
                height = map.Height;
            int row = (from - 1) / width + dRow,
                col = (from - 1) % width + dCol;
            if (row < 0 || row >= height || col < 0 || col >= width)
            {
                return 0;
            }
            return row * width + col + 1;
        }

        /// <summary>
        /// Get the cells path that a ray can reach.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="from"></param>
        /// <param name="first"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static List<int> RayCells(View view, int from, int first, int length)
        {
            var result = new List<int>();
            if (MapRules.Distance(view.Mode, from, first) != 1)
            {
                return result;
            }
            int width = view.Map.Width;
            int dRow = (first - 1) / width - (from - 1) / width;
            int dCol = (first - 1) % width - (from - 1) % width;
            int prev = from;
            for (int k = 0; k < length; ++k)
            {
                int to = Shifted(view.Map, prev, dRow, dCol);
                if (to == 0 || !WorldShot(view, prev, to))
                {
                    break;
                }
                result.Add(to);
                if (view.Buildings[to - 1].Health > 0)
                {
                    break;
                }
                prev = to;
            }
            return result;
        }

        /// <summary>
        /// Judge whether a structure can reach a cell.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool BuildReach(View view, int from, int to)
        {
            if (from < 1 || from > 81 || to < 1 || to > 81)
            {
                return false;
            }
            if (from == to)
            {
                return true;
            }
            var map = Deep.CloneMap(view.Map);
            for (int cell = 1; cell <= 81; ++cell)
            {
                if (map.Cells[cell - 1] == Terrain.Lake)
                {
                    map.Cells[cell - 1] = Terrain.Ground;
                }
                if (view.Buildings[cell - 1].Health > 0 && cell != from && cell != to)
                {
                    map.Cells[cell - 1] = Terrain.Wall;
                }
            }
            int width = view.Map.Width;
            int dRow = (to - 1) / width - (from - 1) / width;
            int dCol = (to - 1) % width - (from - 1) % width;
            if (System.Math.Abs(dRow) > 1 || System.Math.Abs(dCol) > 1)
            {
                return false;
            }
            if (dRow == 0 || dCol == 0)
            {
                return Movement.CanMove(view.Mode, from, to, map);
            }
            int mid1 = (from - 1) / width * width + (to - 1) % width + 1;
            int mid2 = (to - 1) / width * width + (from - 1) % width + 1;
            return Movement.CanMove(view.Mode, from, mid1, map)
                    && Movement.CanMove(view.Mode, mid1, to, map)
                || Movement.CanMove(view.Mode, from, mid2, map)
                    && Movement.CanMove(view.Mode, mid2, to, map);
        }

        /// <summary>
        /// Judge whether a structure can build on a cell.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool BuildSite(View view, int from, int to)
        {
            return MapRules.Valid(view.Mode, to, view.Map)
                && view.Buildings[to - 1].Health <= 0
                && view.Fires[to - 1].Expires == 0
                && view.Rubble[to - 1] == 0
                && BuildReach(view, from, to);
        }

        /// <summary>
        /// Judge whether a structure is a guard wall.
        /// </summary>
        /// <param name="building"></param>
        /// <returns></returns>
        public static bool GuardWall(Building building)
        {
            return building.Health > 0 && building.Kind is Structure.Wall or Structure.GreatWall;
        }

        /// <summary>
        /// Get the cell of a soldier.
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="piece"></param>
        /// <returns></returns>
        public static int SoldierCell(Unit unit, int piece)
        {
            return unit.Role == Role.WallGuard && piece >= 0 && piece < 3
                ? unit.Soldiers[piece]
                : unit.Position;
        }

        /// <summary>
        /// Get the linked guards of a soldier.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="unit"></param>
        /// <param name="piece"></param>
        /// <returns></returns>
        public static int LinkedGuards(Mode mode, Unit unit, int piece)
        {
            if (piece < 0 || piece > 2 || unit.SoldierHealth[piece] <= 0)
            {
                return 0;
            }
            if (unit.SoldierSilenced[piece])
            {
                return 1 << piece;
            }
            int mask = 1 << piece;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (int i in Enumerable.Range(0, 3))
                {
                    if ((mask & (1 << i)) == 0)
                    {
                        continue;
                    }
                    foreach (int j in Enumerable.Range(0, 3))
                    {
                        int width = MapInfo.Cols(mode);
                        bool near =
                            System.Math.Abs(
                                (unit.Soldiers[i] - 1) / width - (unit.Soldiers[j] - 1) / width
                            ) <= 1
                            && System.Math.Abs(
                                (unit.Soldiers[i] - 1) % width - (unit.Soldiers[j] - 1) % width
                            ) <= 1;
                        if (
                            (mask & (1 << j)) == 0
                            && unit.SoldierHealth[j] > 0
                            && !unit.SoldierSilenced[j]
                            && MapRules.Distance(mode, unit.Soldiers[i], unit.Soldiers[j]) == 1
                        )
                        {
                            mask |= 1 << j;
                            changed = true;
                        }
                    }
                }
            }
            return mask;
        }

        /// <summary>
        /// Judge whether a guard formation is valid.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="map"></param>
        /// <param name="anchor"></param>
        /// <param name="cells"></param>
        /// <returns></returns>
        public static bool ValidFormation(Mode mode, Map map, int anchor, int[] cells)
        {
            if (cells == null || cells.Length != 3 || cells[0] != anchor)
            {
                return false;
            }
            foreach (int cell in cells)
            {
                if (!MapRules.Valid(mode, cell, map))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get the guard formation of a soldier.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="anchor"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static int[] GuardSpawn(Mode mode, int anchor, Map map)
        {
            var result = new int[3];
            if (!MapRules.Valid(mode, anchor, map))
            {
                return result;
            }
            result[0] = anchor;
            int count = 1;
            for (int i = 0; i < count && count < 3; ++i)
            {
                for (int cell = 1; cell <= MapInfo.CellCount(mode) && count < 3; ++cell)
                {
                    if (
                        !result[0..3].Contains(cell)
                        && MapRules.Distance(mode, result[i], cell) == 1
                        && Movement.CanMove(mode, result[i], cell, map)
                    )
                    {
                        result[count] = cell;
                        ++count;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get the shadow target of a soldier.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="from"></param>
        /// <param name="dRow"></param>
        /// <param name="dCol"></param>
        /// <returns></returns>
        public static int ShadowTarget(Map map, int from, int dRow, int dCol)
        {
            int width = map.Width,
                height = map.Height;
            int row = (((from - 1) / width + dRow) % height + height) % height;
            int col = (((from - 1) % width + dCol) % width + width) % width;
            return row * width + col + 1;
        }

        /// <summary>
        /// Get the shadow distance between two soldiers.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static int ShadowDistance(Mode mode, int from, int to)
        {
            int width = MapInfo.Cols(mode),
                height = MapInfo.Rows(mode);
            int dRow = System.Math.Abs((from - 1) / width - (to - 1) / width);
            int dCol = System.Math.Abs((from - 1) % width - (to - 1) % width);
            return System.Math.Min(dRow, height - dRow) + System.Math.Min(dCol, width - dCol);
        }

        public static bool ShadowStep(Mode mode, int from, int to, Map map)
        {
            if (
                !MapRules.Valid(mode, from, map)
                || !MapRules.Valid(mode, to, map)
                || ShadowDistance(mode, from, to) > 1
            )
            {
                return false;
            }
            if (MapRules.Distance(mode, from, to) <= 1)
            {
                return Movement.CanMove(mode, from, to, map);
            }
            return true;
        }

        public static bool ShadowShot(Mode mode, int from, int to, Map map)
        {
            if (to < 1 || to > MapInfo.CellCount(mode) || map.Cells[to - 1] == Terrain.Wall)
            {
                return false;
            }
            var shotMap = Deep.CloneMap(map);
            if (shotMap.Cells[to - 1] == Terrain.Lake)
            {
                shotMap.Cells[to - 1] = Terrain.Ground;
            }
            return ShadowStep(mode, from, to, shotMap);
        }

        /// <summary>
        /// The sword qi cap: every sword on the board lowers the cap by one layer.
        /// </summary>
        /// <param name="swords"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static int SwordCap(List<Sword> swords, int id)
        {
            int count = 0;
            foreach (Sword sword in swords)
            {
                if (sword.Unit == id)
                {
                    ++count;
                }
            }
            return System.Math.Max(0, 9 - count);
        }

        /// <summary>
        /// The cells of a sword stab: two cells forward, or self plus the
        /// adjacent cell when <c>includeSelf</c> is set.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="from"></param>
        /// <param name="direction"></param>
        /// <param name="includeSelf"></param>
        /// <returns></returns>
        public static List<int> SwordLine(View view, int from, int direction, bool includeSelf)
        {
            var cells = new List<int>();
            int width = view.Map.Width;
            int dRow = (direction - 1) / width - (from - 1) / width;
            int dCol = (direction - 1) % width - (from - 1) % width;
            if (MapRules.Distance(view.Mode, from, direction) != 1)
            {
                return cells;
            }
            if (includeSelf)
            {
                cells.Add(from);
            }
            int previous = from;
            for (int i = 0; i < (includeSelf ? 1 : 2); ++i)
            {
                int row = (previous - 1) / width + dRow;
                int col = (previous - 1) % width + dCol;
                if (row < 0 || row >= MapInfo.Rows(view.Mode) || col < 0 || col >= width)
                {
                    break;
                }
                int to = row * width + col + 1;
                if (!WorldShot(view, previous, to))
                {
                    break;
                }
                cells.Add(to);
                if (view.Buildings[to - 1].Health > 0)
                {
                    break;
                }
                previous = to;
            }
            return cells;
        }
    }
}
