using System;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    /// <summary>
    /// The board of the game.
    /// </summary>
    public class Board : IClone<Board>
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public LineWalls VerticalWalls;
        public LineWalls HorizontalWalls;

        public readonly Grid[] Grids;

        private Board(Grid[] grids, LineWalls verticalWalls, LineWalls horizontalWalls)
        {
            Grids = grids;
            VerticalWalls = verticalWalls;
            HorizontalWalls = horizontalWalls;
        }

        public static Board New(int height, int width)
        {
            var grids = Enumerable.Range(0, height * width).Select(_ => Grid.New()).ToArray();
            var verticalWalls = LineWalls.WithCapacity(height * (width - 1));
            var horizontalWalls = LineWalls.WithCapacity((height - 1) * width);

            return new(grids, verticalWalls, horizontalWalls) { Height = height, Width = width };
        }

        public static Board? FromMode(GameMode mode, out string? error)
        {
            error = null;
            (int Height, int Width)? size = mode switch
            {
                GameMode.Scout => (ModeConsts.ScoutLength, ModeConsts.ScoutLength),
                GameMode.Heroes => (ModeConsts.HeroesLen, ModeConsts.HeroesLen),
                GameMode.SelfDefine selfDefine => (selfDefine.Height, selfDefine.Width),
                _ => null,
            };

            if (size is null)
            {
                error = "invalid mode";
                return null;
            }

            var (height, width) = size.Value;

            Board board = New(height, width);

            if (mode is GameMode.Heroes)
            {
                Node[] walls = ModeConsts
                    .HeroesWallPos.Select(pos => board.At(pos.X, pos.Y)!)
                    .ToArray();
                foreach (Node wall in walls)
                {
                    board.Grids[wall.pos] = Grid.New().WithOriginalBuilding(Terrain.Wall);
                }
            }
            return board;
        }

        public static Board? FromCode(Box<byte[]> code, out string? error)
        {
            error = null;
            if (ByteReader.New(code) is not ByteReader reader)
            {
                error = "invalid code";
                return null;
            }
            if (reader.ReadInt(out error) is not int width)
            {
                error = $"Board.FromCode Error: {error}";
                return null;
            }
            if (reader.ReadInt(out error) is not int height)
            {
                error = $"Board.FromCode Error: {error}";
                return null;
            }

            Board board = New(height, width);
            int gridCount = board.Grids.Count();
            foreach (int i in Enumerable.Range(0, gridCount))
            {
                if (reader.ReadByte(out error) is not byte terrain)
                {
                    error = $"Board.FromCode Error: {error}";
                    return null;
                }
                if (!Enum.IsDefined(typeof(Terrain), terrain))
                {
                    error = $"invalid terrain {terrain}";
                    return null;
                }
                board.Grids[i] = Grid.New().WithOriginalBuilding((Terrain)terrain);
            }
            foreach (int i in Enumerable.Range(0, board.HorizontalWalls.Inner.Length))
            {
                if (reader.ReadULong(out error) is not ulong value)
                {
                    error = $"Board.FromCode Error: {error}";
                    return null;
                }
                if (!board.HorizontalWalls.SetInner(i, value, out error))
                {
                    error = $"Board.FromCode Error: {error}";
                    return null;
                }
            }
            foreach (int i in Enumerable.Range(0, board.VerticalWalls.Inner.Length))
            {
                if (reader.ReadULong(out error) is not ulong value)
                {
                    error = $"Board.FromCode Error: {error}";
                    return null;
                }
                if (!board.VerticalWalls.SetInner(i, value, out error))
                {
                    error = $"Board.FromCode Error: {error}";
                    return null;
                }
            }
            return board;
        }

        public byte[] IntoCode()
        {
            int gridCount = Grids.Count();
            int codeCount =
                sizeof(int) * 2
                + gridCount
                + HorizontalWalls.Inner.Length * sizeof(ulong)
                + VerticalWalls.Inner.Length * sizeof(ulong);
            byte[] code = new byte[codeCount];
            ByteWriter writer = ByteWriter.New(ref code);
            if (!writer.WriteInt(Width, out _))
            {
                return Array.Empty<byte>();
            }
            if (!writer.WriteInt(Height, out _))
            {
                return Array.Empty<byte>();
            }
            foreach (byte terrain in Grids.Select(grid => (byte)grid.OriginalBuilding))
            {
                if (!writer.WriteByte(terrain, out _))
                {
                    return Array.Empty<byte>();
                }
            }
            foreach (ulong value in HorizontalWalls.Inner)
            {
                if (!writer.WriteULong(value, out _))
                {
                    return Array.Empty<byte>();
                }
            }
            foreach (ulong value in VerticalWalls.Inner)
            {
                if (!writer.WriteULong(value, out _))
                {
                    return Array.Empty<byte>();
                }
            }
            return code;
        }

        public Board Clone()
        {
            return new(
                grids: Grids.Select(grid => grid.Clone()).ToArray(),
                verticalWalls: VerticalWalls.Clone(),
                horizontalWalls: HorizontalWalls.Clone()
            )
            {
                Height = Height,
                Width = Width,
            };
        }

        public Grid? GetGrid(Node node)
        {
            if (!IsValid(node))
            {
                return null;
            }
            return Grids[node.pos];
        }

        public bool SetTerrain(Node node, Terrain terrain)
        {
            if (!IsValid(node))
            {
                return false;
            }
            if (!Enum.IsDefined(typeof(Terrain), terrain))
            {
                return false;
            }
            Grids[node.pos].OriginalBuilding = terrain;
            return true;
        }

        /// <summary>
        /// The node of the map.
        /// </summary>
        public record Node
        {
            /// <summary>
            /// The real id of one position which is converted by map.
            /// </summary>
            internal int pos;

            internal static Node New(int pos) => new() { pos = pos };
        }

        private bool IsValid(Node node)
        {
            return node.pos >= 0 && node.pos < Grids.Count();
        }

        public Node? At(int x, int y)
        {
            if (x < 1 || x > Height)
            {
                return null;
            }
            if (y < 1 || y > Width)
            {
                return null;
            }
            return Node.New((x - 1) * Width + y - 1);
        }

        public int? RowOf(Node node)
        {
            if (!IsValid(node))
            {
                return null;
            }
            return node.pos / Width + 1;
        }

        public int? ColOf(Node node)
        {
            if (!IsValid(node))
            {
                return null;
            }
            return node.pos % Width + 1;
        }

        public int? Distance(Node from, Node to)
        {
            if ((RowOf(from), ColOf(from)) is not (int fromRow, int fromCol))
            {
                return null;
            }
            if ((RowOf(to), ColOf(to)) is not (int toRow, int toCol))
            {
                return null;
            }
            return Math.Abs(fromRow - toRow) + Math.Abs(fromCol - toCol);
        }
    }
}
