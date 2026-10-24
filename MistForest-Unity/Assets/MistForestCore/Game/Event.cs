using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Event
    {
        public virtual bool IsVisibleTo(int playerId) => true;
    }

    public abstract class AttackEvent : Event
    {
        public int AttackerId { get; }
        public int[] UnitIds { get; }
        public int? BuildingId { get; }
        public Node Target { get; }

        protected AttackEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
        {
            AttackerId = attackerId;
            UnitIds = (int[])unitIds.Clone();
            BuildingId = buildingId;
            Target = target;
        }
    }

    public class GeneralMoveEvent : Event
    {
        public int UnitId;
        public Node[] Path;

        private GeneralMoveEvent(Node[] path) => Path = path;

        public static GeneralMoveEvent New(int unitId, Node[] path)
        {
            return new(path) { UnitId = unitId };
        }
    }

    public class GeneralEndEvent : Event
    {
        public int UnitId;

        private GeneralEndEvent(int unitId) => UnitId = unitId;

        public static GeneralEndEvent New(int unitId)
        {
            return new(unitId);
        }
    }

    public class GeneralSurrenderEvent : Event
    {
        public int UnitId;

        private GeneralSurrenderEvent(int unitId) => UnitId = unitId;

        public static GeneralSurrenderEvent New(int unitId)
        {
            return new(unitId);
        }
    }

    public class WhirlpoolAttachEvent : Event
    {
        public int UnitId;
        public Node[] Path;

        private WhirlpoolAttachEvent(Node[] path) => Path = path;

        public static WhirlpoolAttachEvent New(int unitId, Node[] path)
        {
            return new(path) { UnitId = unitId };
        }
    }

    public class GeneralBuildFortEvent : Event
    {
        public int UnitId;
        public Node Target;
        public bool Success;

        private GeneralBuildFortEvent(int unitId, Node target, bool success)
        {
            UnitId = unitId;
            Target = target;
            Success = success;
        }

        public static GeneralBuildFortEvent New(int unitId, Node target, bool success)
        {
            return new(unitId, target, success);
        }
    }

    public class GeneralBuildWallEvent : Event
    {
        public int UnitId;
        public Node[] Targets;
        public bool Success;

        private GeneralBuildWallEvent(int unitId, Node[] targets, bool success)
        {
            UnitId = unitId;
            Targets = targets;
            Success = success;
        }

        public static GeneralBuildWallEvent New(int unitId, Node[] targets, bool success)
        {
            return new(unitId, targets, success);
        }
    }

    public class GeneralDemolishEvent : Event
    {
        public int UnitId;
        public Node Target;

        private GeneralDemolishEvent(int unitId, Node target)
        {
            UnitId = unitId;
            Target = target;
        }

        public static GeneralDemolishEvent New(int unitId, Node target)
        {
            return new(unitId, target);
        }
    }
}
