using System.Collections.Generic;

namespace MistForest.Core.Model
{
    /// <summary>
    /// The action style of bot.
    /// </summary>
    public enum BotStyle
    {
        Balanced,
        Aggressive,
        Cautious,
    }

    /// <summary>
    /// The category of structure.
    /// </summary>
    public enum Structure
    {
        None,
        Fort,
        Wall,
        GreatWall,
    }

    /// <summary>
    /// The building in the game.
    /// </summary>
    public sealed class Building
    {
        public Structure Kind = Structure.None;
        public int? Owner = null;
        public double Health;
        public double DirectDamage;
    }

    /// <summary>
    /// The fire in the game.
    /// </summary>
    public sealed class Fire
    {
        public int? Owner = null;
        public int Expires;
    }

    /// <summary>
    /// The rose in the game.
    /// </summary>
    public sealed class Rose
    {
        public int? Unit = null;
        public int Cell;
        public double Health;
        public int Id;
        public double DirectDamage;
    }

    /// <summary>
    /// The sword in the game, which stays alive until it is called back.
    /// </summary>
    public sealed class Sword
    {
        public int? Unit = null;
        public int Cell;
        public int Spent;
    }

    /// <summary>
    /// The unit in the game.
    /// </summary>
    public sealed class Unit
    {
        public Role Role = Role.Sniper;
        public int Position;
        public int Form;
        public int Cooldown;
        public double Armor;
        public double Health = 2;
        public double Shield;
        public int[] Soldiers = new int[3];
        public double[] SoldierHealth = new double[3];
        public int Ammo;
        public int ArmorLock;
        public bool AmmoBlocked;
        public int[] OptionCooldown = new int[3];
        public bool Silenced,
            Guard,
            WhirlImmune;
        public bool[] SoldierSilenced = new bool[3];
        public bool[] SoldierGuard = new bool[3];
        public bool[] SoldierWhirl = new bool[3];
        public int? Follow = null;
        public int FollowPiece;
        public int Sense = -2; // -2: not sense, -1: no sense target.
        public int SwordQi = 9;
        public string Report = string.Empty;
        public double DirectDamage;
        public double[] SoldierDirectDamage = new double[3];
    }

    /// <summary>
    /// The action has been taken. Not allowed to undo.
    /// </summary>
    public sealed class Turn
    {
        public int? Active = null;
        public int Moves,
            Attacks,
            Used,
            Phase,
            Dr,
            Dc,
            LastOption,
            Options,
            Anchor;
        public int SquadMask,
            SquadDone;
        public bool[] HitCells = new bool[81];
        public bool[] FlameCells = new bool[81];
        public bool FireStep,
            Advance,
            FlameChoice;
        public int ShadowMode,
            ExtraAttacks;
        public bool[] SwordCells = new bool[81];
        public List<int> RoseUsed = new List<int>();
        public bool SwordAdvance,
            SwordBaseUsed;
        public int Pellets;
        public double[] InkDamage = new double[24];
        public bool Led,
            Followed,
            Transformed,
            Dashed,
            DashLast,
            Instinct;
        public List<int> ChargeCells = new List<int>();
    }

    /// <summary>
    /// An action in game to submit.
    /// </summary>
    public sealed class GameAction
    {
        public int Unit;
        public ActionKind Kind = ActionKind.Move;
        public int Target;
        public int? Subject = null;
        public int Piece;
        public int Extra;
        public int Amount = 1;
        public int Landing;
    }

    /// <summary>
    /// The event result in the game.
    /// </summary>
    public sealed class Event
    {
        public int Player,
            Unit,
            Turn;
        public GameAction Action = new GameAction();
        public bool[] AttackCells = new bool[81];
        public string Feedback = string.Empty;
        public string PublicText = string.Empty;
        public string PrivateText = string.Empty;
        public string[] PlayerNotes = new string[4];
    }

    /// <summary>
    /// The broadcast notice in the game.
    /// </summary>
    public sealed class Notice
    {
        public int Revision,
            Turn,
            Player,
            Weight;
        public string Text = string.Empty;
        public bool[] Attacks = new bool[81];
        public int Unit = -1;
        public bool Feedback;
        public ActionKind Kind = ActionKind.Attack;
    }

    /// <summary>
    /// The reaction in the game.
    /// </summary>
    public sealed class Reaction
    {
        public int? Unit;
        public int Piece;
        public int Mask;
        public int[] Health = new int[3];
    }
}
