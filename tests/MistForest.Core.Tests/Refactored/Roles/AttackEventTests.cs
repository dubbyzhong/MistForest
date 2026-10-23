using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class AttackEventTests
    {
        [Theory]
        [InlineData("alpha")]
        [InlineData("wolf")]
        [InlineData("sniper")]
        public static void New_CopiesVictimArrayAndPreservesAttackDetails(string role)
        {
            Board.Node target = Board.New(3, 3).At(2, 2)!.Value;
            int[] victims = { 2, 3 };
            AttackEvent attackEvent = role switch
            {
                "alpha" => AlphaAttackEvent.New(1, victims, 0, target),
                "wolf" => WolfBiteEvent.New(1, victims, 0, target),
                _ => ExecuteEvent.New(1, victims, 0, target),
            };
            victims[0] = 99;

            Assert.Equal(1, attackEvent.AttackerId);
            Assert.Equal(new[] { 2, 3 }, attackEvent.UnitIds);
            Assert.Equal(0, attackEvent.BuildingId);
            Assert.Equal(target, attackEvent.Target);
        }
    }
}
