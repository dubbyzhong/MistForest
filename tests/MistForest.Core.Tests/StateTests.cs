using Map = MistForest.Core.Model.Map;
using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class StateTests
    {
        [Fact]
        public void Unit_Defaults_MatchConventions()
        {
            var unit = new Unit();
            Assert.Equal(2, unit.Health);
            Assert.Equal(0, unit.Armor);
            Assert.Equal(0, unit.Shield);
            Assert.Equal(9, unit.SwordQi);
            Assert.Equal(-2, unit.Sense);
            Assert.Null(unit.Follow);
            Assert.Equal(string.Empty, unit.Report);
            Assert.Equal(0, unit.DirectDamage);
            Assert.Equal(new double[3], unit.SoldierDirectDamage);
        }

        [Fact]
        public void View_Containers_FullyInitialized()
        {
            var view = new View();
            Assert.Equal(81, view.Buildings.Length);
            Assert.NotNull(view.Buildings[80]);

            Assert.Equal(8, view.Units.Length);
            Assert.NotNull(view.Units[7]);
            Assert.Equal(81, view.Possible[3].Length);
            Assert.Equal(1, view.RoundNumber);
            Assert.Null(view.Winner);

            var snapshot = new Snapshot();
            Assert.Equal(4, snapshot.Possible.Length);
            Assert.Equal(8, snapshot.Possible[3].Length);
            Assert.Equal(81, snapshot.Possible[3][7].Length);
        }

        [Theory]
        [InlineData(Role.Sniper, Mode.Heroes, 2)]
        [InlineData(Role.Heavy, Mode.Heroes, 4)]
        [InlineData(Role.WallGuard, Mode.Heroes, 4.8)]
        [InlineData(Role.Wolf, Mode.Heroes, 3)]
        [InlineData(Role.Sword, Mode.Heroes, 3)]
        [InlineData(Role.Sniper, Mode.Scout, 2)]
        [InlineData(Role.Heavy, Mode.Scout, 2)]
        public void InitialHealth_InternalHalfPoints(Role role, Mode mode, double expected)
        {
            Assert.Equal(expected, GameRules.InitialHealth(role, mode));
        }

        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(2, 2, 0)]
        [InlineData(4, 2, 0)]
        [InlineData(1.5, 2, 0.5)]
        public void DirectAttackDelta_OnlyPaysDifference(
            double prior,
            double power,
            double expected
        )
        {
            Assert.Equal(expected, GameRules.DirectAttackDelta(prior, power));
        }

        [Fact]
        public void MapMode_And_Allied()
        {
            Assert.Equal(Mode.Heroes, GameRules.MapMode(new Map()));
            var custom = new Map();
            custom.Height = 7;
            custom.Width = 4;
            Assert.Equal((Mode)74, GameRules.MapMode(custom));

            Assert.True(GameRules.Allied(false, 2, 2));
            Assert.False(GameRules.Allied(false, 1, 3));
            Assert.True(GameRules.Allied(true, 1, 3));
            Assert.True(GameRules.Allied(true, 2, 4));
            Assert.False(GameRules.Allied(true, 3, 4));
        }
    }
}
