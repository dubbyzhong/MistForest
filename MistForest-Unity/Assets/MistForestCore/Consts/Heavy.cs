namespace MistForest.Core.Consts
{
    /// <summary>
    /// Defines Heavy's base values. Health, armor and damage use combat units.
    /// </summary>
    public class HeavyConsts
    {
        public const int HeavyHealth = 2 * CombatConsts.UnitsPerPoint;
        public const int HeavyInitialArmor = CombatConsts.UnitsPerPoint;
        public const int HeavyMoveDistance = 2;
        public const int HeavyChargeMaxDistance = 4;
        public const int HeavyChargeDamage = CombatConsts.UnitsPerPoint;
    }
}
