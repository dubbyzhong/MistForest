namespace MistForest.Core.Consts
{
    public class AlphaConsts
    {
        // Health and damage use quarter-point units.
        public const int AlphaHealth = 3 * CombatConsts.HalfPoint;
        public const int AlphaMoveDistance = 2;
        public const int AlphaAttackRange = 1;
        public const int AlphaAttackDamage = CombatConsts.UnitsPerPoint;
        public const int AlphaBloodCooldownTurns = 2;
    }
}
