namespace MistForest.Core.Consts
{
    /// <summary>
    /// Defines Armiger's base values. Health, armor and damage use quarter-point units.
    /// </summary>
    public class ArmigerConsts
    {
        public const int ArmigerHealth = 3 * CombatConsts.HalfPoint;
        public const int ArmigerMoveDistance = 2;
        public const int ArmigerThrustBaseRange = 1;
        public const int ArmigerThrustDamage = CombatConsts.UnitsPerPoint;
        public const int ArmigerArmorPerLayer = CombatConsts.UnitsPerPoint;
        public const int ArmigerMaxArmor = 2 * ArmigerArmorPerLayer;
        public const int ArmigerArmorLockRounds = 2;
    }
}
