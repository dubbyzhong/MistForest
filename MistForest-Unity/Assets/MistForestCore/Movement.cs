using System.Collections.Generic;

namespace MistForest.Core.Model
{
    /// <summary>
    /// The movement judge of the game.
    /// </summary>
    public static class Movement
    {
        /// <summary>
        /// Judge if the unit can move to the target cell.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool CanMove(Mode mode, int from, int to, Map map)
        {
            if (
                !MapRules.Valid(mode, from, map)
                || !MapRules.Valid(mode, to, map)
                || MapRules.Distance(mode, from, to) > 1
            )
            {
                return false;
            }
            return CanMoveThroughWall(from, to, map);
        }

        private static bool CanMoveThroughWall(int from, int to, Map map)
        {
            if (from == to)
            {
                return true;
            }
            int width = map.Width;
            int fromRow = (from - 1) / width;
            int fromCol = (from - 1) % width;
            int toRow = (to - 1) / width;
            int toCol = (to - 1) % width;
            if (fromRow == toRow)
            {
                return !map.VerticalWalls[fromRow * (width - 1) + System.Math.Min(fromCol, toCol)];
            }
            else
            {
                return !map.HorizontalWalls[System.Math.Min(fromRow, toRow) * width + fromCol];
            }
        }

        /// <summary>
        /// Judge if the segment intersects with the box.
        /// </summary>
        /// <param name="startX"></param>
        /// <param name="startY"></param>
        /// <param name="endX"></param>
        /// <param name="endY"></param>
        /// <param name="top"></param>
        /// <param name="left"></param>
        /// <param name="bottom"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        private static bool SegmentBox(
            double startX,
            double startY,
            double endX,
            double endY,
            double top,
            double left,
            double bottom,
            double right
        )
        {
            const double epsilon = 1e-9;
            if (System.Math.Abs(top - bottom) < epsilon || System.Math.Abs(left - right) < epsilon)
            {
                bool vertical = top == bottom;
                double delta = vertical ? endX - startX : endY - startY;
                if (System.Math.Abs(delta) < epsilon)
                {
                    return false;
                }
                double t = ((vertical ? top : left) - (vertical ? startX : startY)) / delta;
                double along =
                    (vertical ? startY : startX) + t * (vertical ? endY - startY : endX - startX);
                return t > epsilon
                    && t < 1 - epsilon
                    && along > (vertical ? left : top) + epsilon
                    && along < (vertical ? right : bottom) - epsilon;
            }
            top += epsilon;
            left += epsilon;
            bottom -= epsilon;
            right -= epsilon;
            var start = new double[] { startX, startY };
            var end = new double[] { endX, endY };
            var lower = new double[] { top, left };
            var upper = new double[] { bottom, right };
            double minT = 0,
                maxT = 1;
            for (int axis = 0; axis < 2; ++axis)
            {
                double delta = end[axis] - start[axis];
                if (System.Math.Abs(delta) < epsilon)
                {
                    if (start[axis] > upper[axis] || end[axis] < lower[axis])
                    {
                        return false;
                    }
                }
                else
                {
                    double x = (lower[axis] - start[axis]) / delta;
                    double y = (upper[axis] - start[axis]) / delta;
                    if (x > y)
                    {
                        (x, y) = (y, x);
                    }
                    minT = System.Math.Max(minT, x);
                    maxT = System.Math.Min(maxT, y);
                    if (minT > maxT)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Judge if the shot line is clear.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool ClearShot(Mode mode, int a, int b, Map map)
        {
            const double epsilon = 1e-9;
            int width = map.Width,
                height = map.Height;
            if (!MapRules.Valid(mode, a, map) || !MapRules.Valid(mode, b, map))
            {
                return false;
            }
            double startX = (a - 1) / width;
            double startY = (a - 1) % width;
            double endX = (b - 1) / width;
            double endY = (b - 1) % width;
            for (int cell = 1; cell <= width * height; ++cell)
            {
                if (map.Cells[cell - 1] == Terrain.Wall)
                {
                    double cellX = (cell - 1) / width;
                    double cellY = (cell - 1) % width;
                    if (
                        SegmentBox(
                            startX,
                            startY,
                            endX,
                            endY,
                            cellX - 0.5,
                            cellY - 0.5,
                            cellX + 0.5,
                            cellY + 0.5
                        )
                    )
                    {
                        return false;
                    }
                }
            }
            for (int i = 0; i < (height - 1) * width; ++i)
            {
                if (map.HorizontalWalls[i])
                {
                    double x = i / width + 0.5;
                    double y = i % width;
                    if (SegmentBox(startX, startY, endX, endY, x, y - 0.5, x, y + 0.5))
                    {
                        return false;
                    }
                }
            }
            for (int i = 0; i < (width - 1) * height; ++i)
            {
                if (map.VerticalWalls[i])
                {
                    double x = i / (width - 1);
                    double y = i % (width - 1) + 0.5;
                    if (SegmentBox(startX, startY, endX, endY, x - 0.5, y, x + 0.5, y))
                    {
                        return false;
                    }
                }
            }
            if (
                System.Math.Abs(endX - startX) > epsilon
                && System.Math.Abs(endY - startY) > epsilon
            )
            {
                for (int row = 0; row < height - 1; ++row)
                {
                    for (int col = 0; col < width - 1; ++col)
                    {
                        List<(int, int)> vecs = new List<(int, int)>();
                        if (map.VerticalWalls[row * (width - 1) + col])
                        {
                            vecs.Add((-1, 0));
                        }
                        if (map.VerticalWalls[(row + 1) * (width - 1) + col])
                        {
                            vecs.Add((1, 0));
                        }
                        if (map.HorizontalWalls[row * width + col])
                        {
                            vecs.Add((0, -1));
                        }
                        if (map.HorizontalWalls[row * width + col + 1])
                        {
                            vecs.Add((0, 1));
                        }
                        if (vecs.Count < 2)
                        {
                            continue;
                        }
                        double x = row + 0.5,
                            y = col + 0.5;
                        double t = (x - startX) / (endX - startX);

                        if (
                            t > epsilon
                            && t < 1 - epsilon
                            && System.Math.Abs(startY + t * (endY - startY) - y) < epsilon
                        )
                        {
                            if (vecs.Count == 2)
                            {
                                var (x1, y1) = vecs[0];
                                var (x2, y2) = vecs[1];
                                if (x1 * x2 + y1 * y2 != 0)
                                {
                                    return false;
                                }
                                var (x0, y0) = (startX - x, startY - y);
                                double det1 = x1 * x0 + y1 * y0;
                                double det2 = x2 * x0 + y2 * y0;
                                if (det1 * det2 < 0)
                                {
                                    continue;
                                }
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Judge if the shot line is clear. (Override for default map)
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool ClearShot(Mode mode, int a, int b)
        {
            return ClearShot(mode, a, b, MapRules.DefaultMap(mode));
        }

        /// <summary>
        /// Find the dash path from <c>from</c> to <c>to</c>.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static List<int> DashPath(Mode mode, int from, int to, Map map)
        {
            var path = new List<int>();
            if (
                !MapRules.Valid(mode, from, map)
                || !MapRules.Valid(mode, to, map)
                || MapRules.Distance(mode, from, to) != 2
            )
            {
                return path;
            }
            int width = map.Width;
            int[] directions = { -width, width, -1, 1 };
            foreach (int dir in directions)
            {
                if (CanMove(mode, from, from + dir, map) && CanMove(mode, from + dir, to, map))
                {
                    path.Add(from + dir);
                    path.Add(to);
                    return path;
                }
            }
            return path;
        }

        /// <summary>
        /// Judge if the unit can leap from <c>from</c> to <c>to</c>.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool CanLeap(Mode mode, int from, int to, Map map)
        {
            if (!MapRules.Valid(mode, from, map) || !MapRules.Valid(mode, to, map))
            {
                return false;
            }
            int width = map.Width;
            int fromRow = (from - 1) / width,
                fromCol = (from - 1) % width;
            int toRow = (to - 1) / width,
                toCol = (to - 1) % width;
            int dRow = toRow - fromRow,
                dCol = toCol - fromCol;
            bool knightShape = System.Math.Abs(dRow) * System.Math.Abs(dCol) == 2;
            if (!knightShape)
            {
                return false;
            }

            int midRow = fromRow,
                midCol = fromCol;

            if (System.Math.Abs(dRow) == 2)
            {
                midRow += dRow / 2;
            }
            else
            {
                midCol += dCol / 2;
            }
            int mid = midRow * width + midCol + 1;
            return map.Cells[mid - 1] != Terrain.Wall && CanMoveThroughWall(from, mid, map);
        }


    }
}
