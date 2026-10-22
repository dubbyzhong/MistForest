using System;

namespace MistForest.Core.Model
{
    /// <summary>
    /// The encoder and decoder of map.
    /// </summary>
    public class MapCode
    {
        /// <summary>
        /// Encode a map to a string.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static string Encode(Map map)
        {
            var text = new System.Text.StringBuilder();
            text.Append(map.Height.ToString());
            text.Append(map.Width.ToString());
            foreach (Terrain cell in map.Cells)
            {
                text.Append((char)('0' + (int)cell));
            }
            foreach (bool x in map.HorizontalWalls)
            {
                text.Append(x ? '1' : '0');
            }
            foreach (bool x in map.VerticalWalls)
            {
                text.Append(x ? '1' : '0');
            }
            return text.ToString();
        }

        /// <summary>
        /// Try to decode a map from a string.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="text"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool TryDecode(Mode mode, string text, out Map map)
        {
            var next = new Map();
            int cells = 81,
                edges = 72,
                offset = 2;
            if (text.Length == 65)
            {
                cells = 25;
                edges = 20;
                offset = 0;
            }
            else if (text.Length == 227)
            {
                next.Height = text[0] - '0';
                next.Width = text[1] - '0';
            }
            else
            {
                map = new Map();
                return false;
            }
            for (int i = 0; i < cells; ++i)
            {
                char c = text[offset + i];
                if (c < '0' || c > '3')
                {
                    map = new Map();
                    return false;
                }
                next.Cells[i] = (Terrain)(c - '0');
            }
            offset += cells;
            for (int i = 0; i < edges; ++i)
            {
                char h = text[offset + i];
                char v = text[offset + edges + i];
                if (h < '0' || h > '1' || v < '0' || v > '1')
                {
                    map = new Map();
                    return false;
                }
                next.HorizontalWalls[i] = h == '1';
                next.VerticalWalls[i] = v == '1';
            }
            if (!MapRules.ValidMap(mode, next))
            {
                map = new Map();
                return false;
            }
            map = next;
            return true;
        }
    }
}
