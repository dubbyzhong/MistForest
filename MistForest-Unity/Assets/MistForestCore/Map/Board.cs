using System;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    /// <summary>
    /// The board of the game.
    /// </summary>
    public class Board
    {
        public int Width { get; }
        public int Height { get; }
        public LineWalls VerticalWalls;
        public LineWalls HorizontalWalls;

        private readonly Grid[] _grids;

        private Board(int height, int width)
        {
            Height = height;
            Width = width;
            _grids = Enumerable.Range(0, height * width).Select(_ => Grid.New()).ToArray();
            VerticalWalls = LineWalls.WithCapacity(height * (width - 1));
            HorizontalWalls = LineWalls.WithCapacity((height - 1) * width);
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

            var (Height, Width) = size.Value;

            Board board = new(Height, Width);
            if (mode is GameMode.Heroes)
            {
                Node[] walls = ModeConsts
                    .HeroesWallPos.Select(pos => board.At(pos.X, pos.Y)!)
                    .ToArray();
                foreach (Node wall in walls)
                {
                    board._grids[wall.pos] = Grid.New().WithOriginalBuilding(Terrain.Wall);
                }
            }
            return board;
        }

        public static Board? FromCode(byte[] code, out string? error)
        {
            error = null;
            ByteReader reader = ByteReader.New(code);
            if (reader.ReadInt(out error) is not int width)
            {
                error += "\nnot enough bytes to read width.";
                return null;
            }
            if (reader.ReadInt(out error) is not int height)
            {
                error += "\nnot enough bytes to read height.";
                return null;
            }

            Board board = new(height, width);
            int gridCount = board._grids.Count();
            int horizontalWallCount = board.HorizontalWalls.Capacity;
            int verticalWallCount = board.VerticalWalls.Capacity;
            foreach (int i in Enumerable.Range(0, gridCount))
            {
                if (reader.ReadByte(out error) is not byte terrain)
                {
                    error += "\nnot enough bytes to read terrain grids.";
                    return null;
                }
                if (!Enum.IsDefined(typeof(Terrain), terrain))
                {
                    error = $"invalid terrain {terrain}";
                    return null;
                }
                board._grids[i] = Grid.New().WithOriginalBuilding((Terrain)terrain);
            }
            foreach (int i in Enumerable.Range(0, (horizontalWallCount - 1) / sizeof(ulong) + 1))
            {
                if (reader.ReadULong(out error) is not ulong value)
                {
                    error += "\nnot enough bytes to read horizontal walls.";
                    return null;
                }
                if (!board.HorizontalWalls.SetInner(i, value, out _))
                {
                    return null;
                }
            }
            foreach (int i in Enumerable.Range(0, (verticalWallCount - 1) / sizeof(ulong) + 1))
            {
                if (reader.ReadULong(out error) is not ulong value)
                {
                    error += "\nnot enough bytes to read vertical walls.";
                    return null;
                }
                if (!board.VerticalWalls.SetInner(i, value, out _))
                {
                    return null;
                }
            }
            return board;
        }

        public byte[] IntoCode()
        {
            int gridCount = _grids.Count();
            int horizontalWallCount = HorizontalWalls.Capacity;
            int verticalWallCount = VerticalWalls.Capacity;
            int codeCount =
                sizeof(int) * 2
                + gridCount
                + ((horizontalWallCount - 1) / sizeof(ulong) + 1) * sizeof(ulong)
                + ((verticalWallCount - 1) / sizeof(ulong) + 1) * sizeof(ulong);
            byte[] code = new byte[codeCount];
            ByteWriter writer = ByteWriter.New(code);
            if (!writer.WriteInt(Width, out _))
            {
                return Array.Empty<byte>();
            }
            if (!writer.WriteInt(Height, out _))
            {
                return Array.Empty<byte>();
            }
            foreach (byte terrain in _grids.Select(grid => (byte)grid.OriginalBuilding))
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

        public Terrain GetTerrain(Node node)
        {
            return _grids[node.pos].OriginalBuilding;
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
            _grids[node.pos].OriginalBuilding = terrain;
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
            internal readonly int pos;

            internal Node(int Pos) => pos = Pos;
        }

        private bool IsValid(Node node)
        {
            return node.pos >= 0 && node.pos < _grids.Count();
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
            return new Node((x - 1) * Width + y - 1);
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
