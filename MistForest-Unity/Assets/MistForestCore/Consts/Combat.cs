namespace MistForest.Core.Consts
{
    /// <summary>
    /// Scale for refactored combat values. Health, armor and damage are integers
    /// measured in combat units (120 per displayed point); distances, action counts and cooldowns are unscaled.
    /// </summary>
    public class CombatConsts
    {
        public const int UnitsPerPoint = 120;
        public const int HalfPoint = UnitsPerPoint / 2;
        public const int QuarterPoint = UnitsPerPoint / 4;
    }
}
