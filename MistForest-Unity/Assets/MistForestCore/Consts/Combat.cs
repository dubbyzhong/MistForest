namespace MistForest.Core.Consts
{
    /// <summary>
    /// Scale for refactored combat values. Health, armor and damage are integers
    /// measured in quarter points; distances, action counts and cooldowns are unscaled.
    /// </summary>
    public class CombatConsts
    {
        public const int UnitsPerPoint = 4;
        public const int HalfPoint = UnitsPerPoint / 2;
    }
}
