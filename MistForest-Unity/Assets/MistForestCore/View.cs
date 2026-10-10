using System.Collections.Generic;

namespace MistForest.Core.Model
{
    /// <summary>
    /// Construct the model arrays.
    /// </summary>
    internal static class ModelArrays
    {
        /// <summary>
        /// Create a new array of units.
        /// </summary>
        /// <returns></returns>
        public static Unit[] NewUnits()
        {
            var a = new Unit[8];
            for (int i = 0; i < 8; ++i)
            {
                a[i] = new Unit();
            }
            return a;
        }

        /// <summary>
        /// Create a new array of buildings.
        /// </summary>
        /// <returns></returns>
        public static Building[] NewBuildings()
        {
            var a = new Building[81];
            for (int i = 0; i < 81; ++i)
            {
                a[i] = new Building();
            }
            return a;
        }

        /// <summary>
        /// Create a new array of fires.
        /// </summary>
        /// <returns></returns>
        public static Fire[] NewFires()
        {
            var a = new Fire[81];
            for (int i = 0; i < 81; ++i)
            {
                a[i] = new Fire();
            }
            return a;
        }

        /// <summary>
        /// Create a new array of masks.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static bool[][] NewMasks(int count)
        {
            var a = new bool[count][];
            for (int i = 0; i < count; ++i)
            {
                a[i] = new bool[81];
            }
            return a;
        }

        /// <summary>
        /// TODO: possible xxx doesn't know.
        /// Create a new array of possible xxx.
        /// </summary>
        /// <returns></returns>
        public static bool[][][] NewPossible()
        {
            var a = new bool[4][][];
            for (int i = 0; i < 4; ++i)
            {
                a[i] = NewMasks(8);
            }
            return a;
        }
    }

    /// <summary>
    /// The constrained view of the game.
    /// </summary>
    public sealed class View
    {
        public double[] RoundDamage = new double[81];
        public int RoundNumber = 1;
        public int RoundMask;
        public Reaction Reaction = new Reaction();
        public Mode Mode = Mode.Heroes;
        public Map Map = new Map();
        public Building[] Buildings = ModelArrays.NewBuildings();
        public Fire[] Fires = ModelArrays.NewFires();
        public int[] Rubble = new int[81];
        public List<Rose> Roses = new List<Rose>();
        public List<Sword> Swords = new List<Sword>();
        public bool[] Intel = new bool[81];
        public Unit[] Units = ModelArrays.NewUnits();
        public int PlayerCount = 2;
        public bool Teams;
        public bool[] Eliminated = new bool[4];
        public int[] PlayerTurns = new int[4];
        public int TeamSize = 2,
            Player,
            Turn,
            Turns,
            Revision;
        public int? Winner = null;
        public Turn Budget = new Turn();
        public bool[][] Possible = ModelArrays.NewMasks(8);
        public bool[] EnemyAttacks = new bool[81];
        public int? EnemyAttackTurn = null;
        public string SkillFeedback = string.Empty;
        public List<Notice> Notices = new List<Notice>();
        public string Result = string.Empty;
        public string TurnReport = string.Empty;
        public string PreviousReport = string.Empty;
    }

    public sealed class Snapshot
    {
        public double[] RoundDamage = new double[81];
        public int RoundNumber = 1;
        public Reaction Reaction = new Reaction();
        public Mode Mode = Mode.Heroes;
        public Map Map = new Map();
        public Building[] Buildings = ModelArrays.NewBuildings();
        public Fire[] Fires = ModelArrays.NewFires();
        public int[] Rubble = new int[81];
        public List<Rose> Roses = new List<Rose>();
        public List<Sword> Swords = new List<Sword>();
        public bool[][] Intel = ModelArrays.NewMasks(4);
        public Unit[] Units = ModelArrays.NewUnits();
        public int PlayerCount = 2;
        public bool Teams;
        public bool[] Eliminated = new bool[4];
        public int[] PlayerTurns = new int[4];
        public int RoundMask;
        public int TeamSize = 2,
            Turn,
            Turns,
            Revision;
        public int? Winner = null;
        public Turn Budget = new Turn();
        public bool[][][] Possible = ModelArrays.NewPossible();
    }
}
