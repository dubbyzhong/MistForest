using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Fixtures
{
    internal static class GameBuilderTestData
    {
        public static List<PlayerBuilder> MakePlayers(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => New(new List<RoleBuilder> { new DummyRoleBuilderA() })!)
                .ToList();
        }

        public static List<PlayerBuilder> MakeSnipers(Board board, int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ =>
                    New(
                        new List<RoleBuilder>
                        {
                            SniperBuilder.New(
                                new UnitBuilder[] { new SniperUnitBuilder() },
                                board.At(1, 1)!.Value
                            )!,
                        }
                    )!
                )
                .ToList();
        }
    }
}
