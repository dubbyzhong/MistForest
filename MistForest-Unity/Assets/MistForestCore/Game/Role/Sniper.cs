using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored.Roles
{
    public class Sniper : Role
    {
        public Unit Unit { get; private set; }

        private Sniper(Unit unit) => Unit = unit;

        public static Sniper New(Node position)
        {
            return new(Unit.New(2, position));
        }

        public override Role Clone()
        {
            return new Sniper(Unit.Clone());
        }
    }
}
