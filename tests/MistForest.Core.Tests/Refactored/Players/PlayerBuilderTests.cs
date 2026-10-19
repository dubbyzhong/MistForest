using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Tests.Refactored.Fixtures;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Players
{
    public class PlayerBuilderTests
    {
        private static Board.Node AnyNode() => Board.New(1, 1).At(1, 1)!.Value;

        [Fact]
        public static void New_ValidRoleCounts()
        {
            Assert.NotNull(New(new List<RoleBuilder> { new DummyRoleBuilderA() }));
            Assert.NotNull(
                New(new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderB() })
            );
        }

        [Fact]
        public static void New_TooManyRoles_Fails()
        {
            Assert.Null(
                New(
                    new List<RoleBuilder>
                    {
                        new DummyRoleBuilderA(),
                        new DummyRoleBuilderB(),
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            AnyNode()
                        )!,
                    }
                )
            );
        }

        [Fact]
        public static void New_DuplicateRoleTypes_Fails()
        {
            Assert.Null(
                New(new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderA() })
            );
        }

        [Fact]
        public static void Build_ProducesPlayerWithBuiltRoles()
        {
            PlayerBuilder builder = New(
                new List<RoleBuilder> { new DummyRoleBuilderA(), new DummyRoleBuilderB() }
            )!;
            builder.SetId(0);
            Player player = builder.Build()!;
            Assert.IsType<DummyRoleA>(player.Roles[0]);
            Assert.IsType<DummyRoleB>(player.Roles[1]);
        }

        [Fact]
        public static void Player_Clone_DeepCopiesRolesAndId()
        {
            Player player = Player.New(3, new Role[] { new DummyRoleA(), new DummyRoleB() });
            Player clone = player.Clone();
            Assert.NotSame(player, clone);
            Assert.Equal(3, clone.Id);
            Assert.NotSame(player.Roles[0], clone.Roles[0]);
            Assert.IsType<DummyRoleA>(clone.Roles[0]);
            Assert.IsType<DummyRoleB>(clone.Roles[1]);
        }
    }
}
