using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Mapping
{
    public class BoardPathfindingTests
    {
        private static Game CreateGame(Board board)
        {
            GameMode mode = GameMode.SelfDefine.New(board.Height, board.Width);
            Board.Node[] positions = Enumerable
                .Range(1, board.Height)
                .SelectMany(x => Enumerable.Range(1, board.Width).Select(y => board.At(x, y)!.Value))
                .Where(node => board.GetGrid(node)!.CanInitialStay())
                .Take(2)
                .ToArray();
            Assert.Equal(2, positions.Length);

            List<PlayerBuilder> players = positions
                .Select(position =>
                    PlayerBuilder.New(
                        new List<RoleBuilder>
                        {
                            SniperBuilder.New(
                                new UnitBuilder[] { new SniperUnitBuilder() },
                                position
                            )!,
                        }
                    )!
                )
                .ToList();
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        [Fact]
        public static void GetNodesWithinDistance_ZeroDistance_OnlySelf()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            Board.Node from = board.At(3, 3)!.Value;
            var nodes = board.GetNodesWithinDistance(from, 0, 0, CreateGame(board));
            var entry = Assert.Single(nodes);
            Assert.Equal(from, entry.node);
            Assert.Equal(0, entry.distance);
            Assert.Null(entry.lastNode);
        }

        [Fact]
        public static void GetNodesWithinDistance_OpenBoard_Diamond()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            Board.Node from = board.At(3, 3)!.Value;
            var nodes = board.GetNodesWithinDistance(from, 2, 0, CreateGame(board));
            Assert.Equal(13, nodes.Length);
            foreach (var (node, distance, lastNode) in nodes)
            {
                Assert.Equal(board.Distance(from, node), distance);
                if (distance == 0)
                {
                    Assert.Null(lastNode);
                }
                else
                {
                    Assert.NotNull(lastNode);
                    Assert.Equal(distance - 1, board.Distance(from, lastNode!.Value));
                }
            }
        }

        [Fact]
        public static void GetNodesWithinDistance_VerticalWall_Blocks()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(3, 3), out _)!;
            board.VerticalWalls.SetInner(0, 1ul << 2, out _);
            Board.Node from = board.At(2, 1)!.Value;
            var reached = board
                .GetNodesWithinDistance(from, maxDistance: 2, playerId: 0, CreateGame(board))
                .Select(entry => entry.node)
                .ToHashSet();
            Assert.Equal(5, reached.Count);
            Assert.Contains(board.At(1, 1)!.Value, reached);
            Assert.Contains(board.At(3, 1)!.Value, reached);
            Assert.Contains(board.At(1, 2)!.Value, reached);
            Assert.Contains(board.At(3, 2)!.Value, reached);
            Assert.DoesNotContain(board.At(2, 2)!.Value, reached);
            Assert.DoesNotContain(board.At(2, 3)!.Value, reached);
        }

        [Fact]
        public static void GetNodesWithinDistance_HorizontalWall_Blocks()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(3, 3), out _)!;
            board.HorizontalWalls.SetInner(0, 1ul << 0, out _);
            Board.Node from = board.At(1, 1)!.Value;
            var reached = board
                .GetNodesWithinDistance(from, maxDistance: 2, playerId: 0, CreateGame(board))
                .Select(entry => entry.node)
                .ToHashSet();
            Assert.DoesNotContain(board.At(2, 1)!.Value, reached);
            Assert.Contains(board.At(1, 2)!.Value, reached);
            Assert.Contains(board.At(2, 2)!.Value, reached);
        }

        [Fact]
        public static void GetNodesWithinDistance_Lake_BlocksPathNoDetour()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(3, 3), out _)!;
            board.SetTerrain(board.At(2, 2)!.Value, Terrain.Lake);
            Board.Node from = board.At(1, 2)!.Value;
            var reached = board
                .GetNodesWithinDistance(from, 2, 0, CreateGame(board))
                .Select(entry => entry.node)
                .ToHashSet();
            Assert.Equal(5, reached.Count);
            Assert.DoesNotContain(board.At(2, 2)!.Value, reached);
            Assert.DoesNotContain(board.At(3, 2)!.Value, reached);
        }

        [Fact]
        public static void GetNodesWithinDistance_BottomRow_NoWallIndexCrash()
        {
            Board board = Board.FromMode(GameMode.Heroes.Instance, out _)!;
            Board.Node from = board.At(1, 1)!.Value;
            var reached = board
                .GetNodesWithinDistance(from, 8, 0, CreateGame(board))
                .Select(entry => entry.node)
                .ToHashSet();
            Assert.Contains(board.At(5, 4)!.Value, reached);
            Assert.Contains(board.At(5, 2)!.Value, reached);
            Assert.Equal(21, reached.Count);
        }
    }
}
