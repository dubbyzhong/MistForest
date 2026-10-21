using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using GameAction = MistForest.Core.GameRefactored.Action;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Fixtures
{
    public sealed class DummyRoleA : Role
    {
        public static DummyRoleA New(int id, int playerId)
        {
            DummyRoleA target = new();
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone() => New(Id, PlayerId);

        public override bool HasActionLeft(Game game) => false;

        public override ReadOnlySpan<Unit> GetUnits() => Array.Empty<Unit>();

        public override void ExecuteAction(GameAction action, Game game) { }

        public override bool JudgeAction(GameAction action, Game game) => false;
    }

    public sealed class DummyRoleB : Role
    {
        public static DummyRoleB New(int id, int playerId)
        {
            DummyRoleB target = new();
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone() => New(Id, PlayerId);

        public override bool HasActionLeft(Game game) => false;

        public override ReadOnlySpan<Unit> GetUnits() => Array.Empty<Unit>();

        public override void ExecuteAction(GameAction action, Game game) { }

        public override bool JudgeAction(GameAction action, Game game) => false;
    }

    public sealed class DummyRoleC : Role
    {
        public static DummyRoleC New(int id, int playerId)
        {
            DummyRoleC target = new();
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone() => New(Id, PlayerId);

        public override bool HasActionLeft(Game game) => false;

        public override ReadOnlySpan<Unit> GetUnits() => Array.Empty<Unit>();

        public override void ExecuteAction(GameAction action, Game game) { }

        public override bool JudgeAction(GameAction action, Game game) => false;
    }

    public sealed class DummyRoleBuilderA : RoleBuilder
    {
        public DummyRoleBuilderA()
            : base(new UnitBuilder[] { }) { }

        public override bool CanInitialStay(Board board) => true;

        public override Role? Build()
        {
            if (Built is not null)
            {
                return Built;
            }
            if (Id is not int id || PlayerId is not int playerId)
            {
                return null;
            }
            return Built = DummyRoleA.New(id, playerId);
        }
    }

    public sealed class DummyRoleBuilderB : RoleBuilder
    {
        public DummyRoleBuilderB()
            : base(new UnitBuilder[] { }) { }

        public override bool CanInitialStay(Board board) => true;

        public override Role? Build()
        {
            if (Built is not null)
            {
                return Built;
            }
            if (Id is not int id || PlayerId is not int playerId)
            {
                return null;
            }
            return Built = DummyRoleB.New(id, playerId);
        }
    }
}
