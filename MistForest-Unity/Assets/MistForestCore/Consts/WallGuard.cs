namespace MistForest.Core.Consts
{
    public static class WallGuardConsts
    {
        public const int SoldierCount = 3;
        public const int SoldierHealth = 4 * (CombatConsts.UnitsPerPoint / 5);
        public const int MoveDistance = 1;
        public const int GroundAttackRange = 1;
        public const int WallAttackRange = 2;
        public const int AttackDamage = CombatConsts.UnitsPerPoint;
        public const int GreatWallHealth = CombatConsts.UnitsPerPoint;
    }
}
