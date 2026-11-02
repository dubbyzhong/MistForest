namespace MistForest.Core.GameRefactored
{
    public enum DamageType
    {
        Melee,
        Ranged,
    }

    /// <summary>
    /// Records actual armor, temporary shield and health loss in quarter-point units.
    /// </summary>
    public readonly struct DamageResult
    {
        public int ArmorDamage { get; }
        public int ShieldDamage { get; }
        public int HealthDamage { get; }
        public int TotalDamage => ArmorDamage + ShieldDamage + HealthDamage;

        internal DamageResult(int armorDamage, int shieldDamage, int healthDamage)
        {
            ArmorDamage = armorDamage;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
        }
    }
}
