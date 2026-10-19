using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.Tests.Refactored.Fixtures.GameBuilderTestData;

namespace MistForest.Core.Tests.Refactored.GameSetup
{
    public class TurnOrderTests
    {
        [Fact]
        public static void SetFirstPlayerId_OutOfRange_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Assert.NotNull(builder.SetPlayers(Box.New(MakeSnipers(board, 2))));
            Assert.Null(builder.SetFirstPlayerId(-1));
            Assert.Null(builder.SetFirstPlayerId(2));
            Assert.NotNull(builder.SetFirstPlayerId(0));
            Assert.NotNull(builder.SetFirstPlayerId(1));
        }

        [Fact]
        public static void NextPlayerId_SingleGame_Cycles()
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(5, 5))));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(MakePlayers(2))));
            Game game = builder.Build()!;
            Assert.Equal(1, game.NextPlayerId(0));
            Assert.Equal(0, game.NextPlayerId(1));
        }

        [Fact]
        public static void NextPlayerId_TeamGame_CyclesThroughAllSeats()
        {
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.NotNull(builder.SetTeams(Box.New(teams)));
            Game game = builder.Build()!;
            Assert.Equal(2, game.NextPlayerId(0));
            Assert.Equal(3, game.NextPlayerId(1));
            Assert.Equal(1, game.NextPlayerId(2));
            Assert.Equal(0, game.NextPlayerId(3));

            HashSet<int> visited = new();
            int seat = 0;
            for (int i = 0; i < 4; ++i)
            {
                seat = game.NextPlayerId(seat);
                visited.Add(seat);
            }
            Assert.Equal(4, visited.Count);
        }
    }
}
