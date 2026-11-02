namespace MistForest.Core.Consts
{
    /// <summary>
    /// Defines Ink's base values. Health and damage are measured in quarter-point units.
    /// </summary>
    public class InkConsts
    {
        public const int InkHealth = 3 * CombatConsts.HalfPoint;
        public const int InkMoveDistance = 1;
        public const int InkAttackRange = 1;
        public const int InkMaxAttackAction = 3;
        public const int InkAttackDamage = CombatConsts.HalfPoint;
        public const int InkBuildingAttackDamage = CombatConsts.UnitsPerPoint;
        public const int InkDashDistance = 2;
        public const int InkDashCooldownRounds = 2;
    }
}
