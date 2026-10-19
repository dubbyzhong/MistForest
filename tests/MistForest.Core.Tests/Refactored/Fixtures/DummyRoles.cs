using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Fixtures
{
    public sealed class DummyRoleA : Role
    {
        public override Role Clone() => new DummyRoleA();
        public override bool HasActionLeft() => false;
        public override Unit[] GetUnits() => new Unit[0];
    }

    public sealed class DummyRoleB : Role
    {
        public override Role Clone() => new DummyRoleB();
        public override bool HasActionLeft() => false;
        public override Unit[] GetUnits() => new Unit[0];
    }

    public sealed class DummyRoleC : Role
    {
        public override Role Clone() => new DummyRoleC();
        public override bool HasActionLeft() => false;
        public override Unit[] GetUnits() => new Unit[0];
    }

    public sealed class DummyRoleBuilderA : RoleBuilder
    {
        public DummyRoleBuilderA()
            : base(new UnitBuilder[] { }) { }

        public override bool CanInitialStay(Board board) => true;

        public override Role Build() => new DummyRoleA();
    }

    public sealed class DummyRoleBuilderB : RoleBuilder
    {
        public DummyRoleBuilderB()
            : base(new UnitBuilder[] { }) { }

        public override bool CanInitialStay(Board board) => true;

        public override Role Build() => new DummyRoleB();
    }
}
