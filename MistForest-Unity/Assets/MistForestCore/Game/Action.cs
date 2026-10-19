using MistForest.Core.GameRefactored.Roles;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Action { }

    public class GeneralMove : Action, ISniperAction
    {
        public Node Target { get; private set; }

        public static GeneralMove New(Node target)
        {
            return new() { Target = target };
        }
    }

    public class GeneralEnd : Action, ISniperAction { }

    public class GeneralSurrender : Action, ISniperAction { }
}
