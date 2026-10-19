using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SniperBuilderTests
    {
        private static Sniper BuildSniper(Board.Node node)
        {
            SniperBuilder builder = SniperBuilder.New(
                new UnitBuilder[] { new SniperUnitBuilder() },
                node
            )!;
            builder.SetId(3);
            builder.SetPlayerId(1);
            builder.UnitBuilder[0].SetRoleId(3);
            builder.UnitBuilder[0].SetId(7);
            return (Sniper)builder.Build()!;
        }

        [Fact]
        public static void SniperBuilder_Build_CreatesUnitWithHealthAndPosition()
        {
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Board.Node node = board.At(2, 3)!.Value;
            Sniper sniper = BuildSniper(node);
            Assert.Equal(SniperConsts.SniperHealth, sniper.Unit.Health);
            Assert.Equal(node, sniper.Unit.Position);
            Assert.Equal(3, sniper.Id);
            Assert.Equal(1, sniper.PlayerId);
        }

        [Fact]
        public static void Sniper_Clone_CopiesUnitAndIdentity()
        {
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            Sniper sniper = BuildSniper(board.At(1, 1)!.Value);
            Sniper clone = (Sniper)sniper.Clone();
            Assert.NotSame(sniper.Unit, clone.Unit);
            Assert.Equal(sniper.Unit.Health, clone.Unit.Health);
            Assert.Equal(sniper.Unit.Position, clone.Unit.Position);
            Assert.Equal(sniper.Id, clone.Id);
            Assert.Equal(sniper.PlayerId, clone.PlayerId);
        }

        [Fact]
        public static void SniperBuilder_CanInitialStay_FollowsTerrain()
        {
            Board heroes = Board.FromMode(GameMode.Heroes.Instance, out _)!;
            Assert.False(
                SniperBuilder
                    .New(new UnitBuilder[] { new SniperUnitBuilder() }, heroes.At(1, 1)!.Value)!
                    .CanInitialStay(heroes)
            );
            Assert.True(
                SniperBuilder
                    .New(new UnitBuilder[] { new SniperUnitBuilder() }, heroes.At(1, 2)!.Value)!
                    .CanInitialStay(heroes)
            );

            Board custom = Board.FromMode(GameMode.SelfDefine.New(3, 3), out _)!;
            custom.SetTerrain(custom.At(2, 2)!.Value, Terrain.Lake);
            custom.SetTerrain(custom.At(3, 3)!.Value, Terrain.Whirlpool);
            Assert.False(
                SniperBuilder
                    .New(new UnitBuilder[] { new SniperUnitBuilder() }, custom.At(2, 2)!.Value)!
                    .CanInitialStay(custom)
            );
            Assert.True(
                SniperBuilder
                    .New(new UnitBuilder[] { new SniperUnitBuilder() }, custom.At(3, 3)!.Value)!
                    .CanInitialStay(custom)
            );
        }
    }
}
