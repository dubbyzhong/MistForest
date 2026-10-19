using MistForest.Core.GameRefactored;
using MistForest.Core.Tests.Refactored.Fixtures;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Players
{
    public class TeamTests
    {
        [Fact]
        public static void New_RequiresExactlyTwoPlayers()
        {
            PlayerBuilder a = New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;
            PlayerBuilder b = New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;
            PlayerBuilder c = New(new List<RoleBuilder> { new DummyRoleBuilderA() })!;

            Assert.NotNull(Team.New(new List<PlayerBuilder> { a, b }));
            Assert.Null(Team.New(new List<PlayerBuilder> { a }));
            Assert.Null(Team.New(new List<PlayerBuilder> { a, b, c }));
        }
    }
}
