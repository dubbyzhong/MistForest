using System.Collections.Generic;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Event { }

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
}
