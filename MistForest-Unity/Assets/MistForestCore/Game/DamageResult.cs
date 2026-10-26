namespace MistForest.Core.GameRefactored
{
    /// <summary>
    /// Records actual armor and health loss in half-point units, excluding excess damage.
    /// </summary>
    public readonly struct DamageResult
    {
        public int ArmorDamage { get; }
        public int HealthDamage { get; }
        public int TotalDamage => ArmorDamage + HealthDamage;

        internal DamageResult(int armorDamage, int healthDamage)
        {
            ArmorDamage = armorDamage;
            HealthDamage = healthDamage;
        }
    }
}
