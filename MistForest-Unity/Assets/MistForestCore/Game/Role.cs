using System.Collections.Generic;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Role : IClone<Role>
    {
        public abstract Role Clone();
    }

    public class Unit : IClone<Unit>
    {
        public int Health { get; private set; }
        public Node Position { get; private set; }

        private Unit(Node position) => Position = position;

        public static Unit New(int health, Node position)
        {
            return new Unit(position) { Health = health };
        }

        public Unit Clone()
        {
            return New(Health, Position);
        }
    }
}
