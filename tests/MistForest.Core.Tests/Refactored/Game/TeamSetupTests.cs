using System.Linq;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.Tests.Refactored.Fixtures.GameBuilderTestData;

namespace MistForest.Core.Tests.Refactored.GameSetup
{
    public class TeamSetupTests
    {
        [Fact]
        public static void SetTeams_ScoutMode_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            Assert.Null(builder.SetTeams(Box.New(teams)));
        }

        [Fact]
        public static void SetTeams_WithoutModeAndBoard_Fails()
        {
            GameBuilder builder = new();
            List<PlayerBuilder> players = MakePlayers(4);
            List<Team> teams = new()
            {
                Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                Team.New(new List<PlayerBuilder> { players[2], players[3] })!,
            };
            Assert.Null(builder.SetTeams(Box.New(teams)));
        }

        [Fact]
        public static void Build_TeamGame_AssignsTeamIds()
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
            Assert.NotNull(builder.Build());

            Assert.Equal(0, players[0].TeamId);
            Assert.Equal(0, players[1].TeamId);
            Assert.Equal(1, players[2].TeamId);
            Assert.Equal(1, players[3].TeamId);

            Assert.Null(builder.SetPlayers(Box.New(MakePlayers(2))));

            GameBuilder second = new();
            Assert.NotNull(second.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)));
            Assert.Null(second.SetPlayers(Box.New(players)));
        }

        [Fact]
        public static void SetTeams_InvalidTeamCount_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            List<PlayerBuilder> players = MakePlayers(2);
            Assert.Null(
                builder.SetTeams(
                    Box.New(
                        new List<Team>
                        {
                            Team.New(new List<PlayerBuilder> { players[0], players[1] })!,
                        }
                    )
                )
            );

            List<PlayerBuilder> six = MakePlayers(6);
            List<Team> threeTeams = Enumerable
                .Range(0, 3)
                .Select(i => Team.New(new List<PlayerBuilder> { six[i * 2], six[i * 2 + 1] })!)
                .ToList();
            GameBuilder second = new();
            Assert.NotNull(second.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.Null(second.SetTeams(Box.New(threeTeams)));
        }

        [Fact]
        public static void SetTeams_EmptyBox_Fails()
        {
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.Heroes.Instance)));
            Assert.Null(builder.SetTeams(Box<List<Team>>.Empty));
        }
    }
}
