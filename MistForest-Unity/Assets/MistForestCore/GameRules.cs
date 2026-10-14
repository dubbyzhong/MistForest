namespace MistForest.Core.Model
{
    /// <summary>
    /// The game rules.
    /// </summary>
    public static class GameRules
    {
        /// <summary>
        /// Get the mode of the map.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static Mode MapMode(Map map)
        {
            return map.Width == 5 && map.Height == 5
                ? Mode.Heroes
                : (Mode)(map.Height * 10 + map.Width);
        }

        /// <summary>
        /// Check if two units are allied.
        /// </summary>
        /// <param name="teams"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool Allied(bool teams, int a, int b)
        {
            return a == b || (teams && a % 2 == b % 2);
        }

        /// <summary>
        /// Check if the mode is a hero mode.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static bool HeroMode(Mode mode)
        {
            return mode != Mode.Scout;
        }

        /// <summary>
        /// Calculate the direct attack delta.
        /// </summary>
        /// <param name="prior"></param>
        /// <param name="power"></param>
        /// <returns></returns>
        public static double DirectAttackDelta(double prior, double power)
        {
            return System.Math.Max(0, power - prior);
        }

        /// <summary>
        /// Get the initial health of the unit.
        /// </summary>
        /// <param name="role"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static double InitialHealth(Role role, Mode mode)
        {
            return !HeroMode(mode) || role == Role.Sniper ? 2
                : role == Role.Heavy ? 4
                : role == Role.WallGuard ? 4.8
                : 3;
        }

        public static bool SilentArea(Mode mode, int a, int b, Map map)
        {
            if (!MapRules.Valid(mode, a, map) || !MapRules.Valid(mode, b, map))
            {
                return false;
            }
            return MapRules.Distance(mode, a, b) <= 1;
        }

        public static bool Immune(Unit unit)
        {
            return unit.Role == Role.HiddenWolf && unit.Form == 0;
        }

        public static int AttackRange(Mode mode, Unit unit)
        {
            return mode == Mode.Scout ? 1
                : unit.Role == Role.Sniper ? 3
                : unit.Role == Role.HiddenWolf && unit.Form == 0 ? 2
                : 1;
        }

        public static double ArmorUnload(double armor, double choice)
        {
            const double epsilon = 1e-9;
            double fraction = armor - System.Math.Floor(armor + epsilon);
            if (fraction < epsilon)
            {
                fraction = 0; // 整数甲:小数部分为零,继续按整数层卸载
            }
            double amount = fraction + choice;
            if (fraction > 0 && choice > 1)
            {
                return 0;
            }
            return amount > 0 && amount <= armor + epsilon ? amount : 0;
        }
    }
}
