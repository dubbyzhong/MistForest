using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameRefactored;
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
        public GameRandom Random;

        private static readonly (int, int)[] _moveDirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private Board(
            Grid[] grids,
            LineWalls verticalWalls,
            LineWalls horizontalWalls,
            GameRandom random
        )
        {
            Grids = grids;
            VerticalWalls = verticalWalls;
            HorizontalWalls = horizontalWalls;
            Random = random;
        }

        public static Board New(int height, int width)
        {
            var grids = Enumerable.Range(0, height * width).Select(_ => Grid.New()).ToArray();
            var verticalWalls = LineWalls.WithCapacity(height * (width - 1));
            var horizontalWalls = LineWalls.WithCapacity((height - 1) * width);

            Guid guid = Guid.NewGuid();
            ByteReader reader = ByteReader.New(Box.New(guid.ToByteArray()))!;

            return new(
                grids,
                verticalWalls,
                horizontalWalls,
                random: GameRandom.New(reader.ReadULong(out _) ?? (ulong)guid.GetHashCode())!.Value
            )
            {
                Height = height,
                Width = width,
            };
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
                    .HeroesWallPos.Select(pos => board.At(pos.X, pos.Y)!.Value)
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
            foreach (byte terrain in Grids.Select(grid => (byte)grid.Terrain))
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
                horizontalWalls: HorizontalWalls.Clone(),
                random: Random
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
            Grids[node.pos].Terrain = terrain;
            return true;
        }

        /// <summary>
        /// The node of the map.
        /// </summary>
        public record struct Node
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

        public int? HWallAt(Node node)
        {
            if (RowOf(node) is not int row)
            {
                return null;
            }
            if (ColOf(node) is not int col)
            {
                return null;
            }
            if (row == Height)
            {
                return null;
            }
            return (row - 1) * Width + col - 1;
        }

        public int? VWallAt(Node node)
        {
            if (RowOf(node) is not int row)
            {
                return null;
            }
            if (ColOf(node) is not int col)
            {
                return null;
            }
            if (col == Width)
            {
                return null;
            }
            return (row - 1) * (Width - 1) + col - 1;
        }

        public Node? Move(Node from, int dx, int dy)
        {
            if (!IsValid(from))
            {
                return null;
            }
            if ((RowOf(from), ColOf(from)) is not (int fromRow, int fromCol))
            {
                return null;
            }
            int toRow = fromRow + dx;
            int toCol = fromCol + dy;
            return At(toRow, toCol);
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

        public (Node node, int distance, Node? lastNode)[] GetNodesWithinDistance(
            Node from,
            int maxDistance,
            int playerId,
            Game game
        )
        {
            Queue<(Node node, int distance, Node? lastNode)> queue = new();
            List<(Node node, int distance, Node? lastNode)> result = new();
            HashSet<Node> visited = new();
            queue.Enqueue((from, 0, null));
            visited.Add(from);
            while (queue.Count > 0)
            {
                var (node, distance, lastNode) = queue.Dequeue();
                result.Add((node, distance, lastNode));
                if (distance >= maxDistance)
                {
                    continue;
                }
                foreach ((int dx, int dy) in _moveDirs)
                {
                    if (Move(node, dx, dy) is not Node to)
                    {
                        continue;
                    }
                    if (visited.Contains(to))
                    {
                        continue;
                    }
                    if (dx == 0 && VerticalWalls[VWallAt(dy == 1 ? node : to)!.Value])
                    {
                        continue;
                    }
                    if (dy == 0 && HorizontalWalls[HWallAt(dx == 1 ? node : to)!.Value])
                    {
                        continue;
                    }
                    if (!GetGrid(to)!.CanStay(playerId, game))
                    {
                        continue;
                    }
                    queue.Enqueue((to, distance + 1, node));
                    visited.Add(to);
                }
            }
            return result.ToArray();
        }

        public bool CanLineShot(Node from, Node to, bool diagonalBlocked)
        {
            if (from == to)
            {
                return true;
            }
            int fromRow = RowOf(from)!.Value;
            int fromCol = ColOf(from)!.Value;
            int toRow = RowOf(to)!.Value;
            int toCol = ColOf(to)!.Value;
            int dx = toRow - fromRow;
            int dy = toCol - fromCol;
            int stepX = Math.Sign(toRow - fromRow);
            int stepY = Math.Sign(toCol - fromCol);

            double tx = double.PositiveInfinity;
            double ty = double.PositiveInfinity;
            double tdx = double.PositiveInfinity;
            double tdy = double.PositiveInfinity;

            if (stepX != 0)
            {
                int nextEdgeX = stepX > 0 ? fromRow : fromRow - 1;
                tx = (nextEdgeX + 0.5 - fromRow) / dx;
                tdx = Math.Abs(1.0 / dx);
            }
            if (stepY != 0)
            {
                int nextEdgeY = stepY > 0 ? fromCol : fromCol - 1;
                ty = (nextEdgeY + 0.5 - fromCol) / dy;
                tdy = Math.Abs(1.0 / dy);
            }

            const double Epsilon = 1e-9;
            int maxSteps = dx + dy + 2;
            int x = fromRow;
            int y = fromCol;

            for (int i = 0; i < maxSteps && (x != toRow || y != toCol); ++i)
            {
                if (tx < ty - Epsilon)
                {
                    int edgeX = stepX > 0 ? fromRow : fromRow - 1;
                    if (HorizontalWalls[HWallAt(At(edgeX, y)!.Value)!.Value])
                    {
                        return false;
                    }
                    x += stepX;
                    Node node = At(x, y)!.Value;
                    if (node != to && !GetGrid(node)!.CanShot())
                    {
                        return false;
                    }
                    tx += tdx;
                }
                else if (ty < tx - Epsilon)
                {
                    int edgeY = stepY > 0 ? fromCol : fromCol - 1;
                    if (VerticalWalls[VWallAt(At(x, edgeY)!.Value)!.Value])
                    {
                        return false;
                    }
                    y += stepY;
                    Node node = At(x, y)!.Value;
                    if (node != to && !GetGrid(node)!.CanShot())
                    {
                        return false;
                    }
                    ty += tdy;
                }
                else
                {
                    int edgeX = stepX > 0 ? fromRow : fromRow - 1;
                    int edgeY = stepY > 0 ? fromCol : fromCol - 1;

                    bool wallV1 = VerticalWalls[VWallAt(At(x, edgeY)!.Value)!.Value];
                    bool wallH1 = HorizontalWalls[HWallAt(At(edgeX, y + stepY)!.Value)!.Value];
                    bool wallH2 = HorizontalWalls[HWallAt(At(edgeX, y)!.Value)!.Value];
                    bool wallV2 = VerticalWalls[VWallAt(At(x + stepX, edgeY)!.Value)!.Value];

                    bool pathClear1 = !wallV1 && !wallH1;
                    bool pathClear2 = !wallV2 && !wallH2;

                    bool blocked = diagonalBlocked
                        ? !pathClear1 && !pathClear2
                        : !pathClear1 || !pathClear2;

                    if (blocked)
                    {
                        return false;
                    }
                    x += stepX;
                    y += stepY;
                    Node node = At(x, y)!.Value;
                    if (node != to && !GetGrid(node)!.CanShot())
                    {
                        return false;
                    }
                    tx += tdx;
                    ty += tdy;
                }
            }
            return true;
        }
    }
}
