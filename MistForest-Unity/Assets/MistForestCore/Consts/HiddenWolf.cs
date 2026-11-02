namespace MistForest.Core.Consts
{
    public class HiddenWolfConsts
    {
        // Health and damage use quarter-point units.
        public const int HiddenWolfHealth = 3 * CombatConsts.HalfPoint;
        public const int HiddenWolfHumanMoveDistance = 1;
        public const int HiddenWolfHumanAttackRange = 2;
        public const int HiddenWolfWolfMoveDistance = 2;
        public const int HiddenWolfWolfAttackRange = 1;
        public const int HiddenWolfPreAttackMoveDistance = 1;
        public const int HiddenWolfPostAttackMoveDistance = 1;
        public const int HiddenWolfHumanShield = CombatConsts.HalfPoint;
        public const int HiddenWolfAttackDamage = CombatConsts.UnitsPerPoint;
    }
}
