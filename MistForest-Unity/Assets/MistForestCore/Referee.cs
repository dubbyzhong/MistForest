using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MistForest.Core.Model
{
    /// <summary>
    /// The game judge which manages the game process.
    /// </summary>
    public sealed class Game
    {
        private readonly Snapshot state = new Snapshot();
        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private readonly List<Event> events = new List<Event>();

        public Snapshot State
        {
            get { return state; }
        }

        public IReadOnlyList<Snapshot> Snapshots
        {
            get { return snapshots; }
        }

        public IReadOnlyList<Event> Events
        {
            get { return events; }
        }
        public bool Finished
        {
            get { return state.Winner != null; }
        }

        /// <summary>
        /// Initializes a new game of Mist Forest.
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="roles"></param>
        /// <param name="mode"></param>
        /// <param name="first"></param>
        /// <param name="map"></param>
        /// <param name="teamSize"></param>
        /// <param name="playerCount"></param>
        /// <param name="teams"></param>
        public Game(
            int[] positions,
            Role[] roles,
            Mode mode,
            int first,
            Map map,
            int teamSize,
            int playerCount,
            bool teams
        )
        {
            map = map ?? MapRules.DefaultMap(mode);
            if (
                !MapRules.ValidMap(mode, map)
                || playerCount < 2
                || playerCount > 4
                || first < 0
                || first >= playerCount
                || teamSize < 1
                || teamSize > 2
                || (teams && playerCount != 4)
            )
            {
                throw new ArgumentException("setup is invalid");
            }
            mode = mode == Mode.Scout ? mode : GameRules.MapMode(map);
            state.Mode = mode;
            state.Map = Deep.CloneMap(map);
            state.PlayerCount = playerCount;
            state.Teams = teams;
            state.TeamSize = GameRules.HeroMode(mode) ? teamSize : 1;
            state.Turn = first;
            for (int i = 0; i < 8; ++i)
            {
                Unit unit = state.Units[i];
                if (i / 2 >= playerCount || i % 2 >= state.TeamSize)
                {
                    unit.Health = 0;
                    continue;
                }
                if (
                    !Enum.IsDefined(typeof(Role), roles[i])
                    || !MapRules.Valid(mode, positions[i], state.Map)
                )
                {
                    throw new ArgumentException("unit is invalid");
                }
                unit.Role = GameRules.HeroMode(mode) ? roles[i] : Role.Sniper;
                unit.Position = positions[i];
                unit.Health = GameRules.InitialHealth(unit.Role, mode);
                unit.Armor = GameRules.HeroMode(mode) && unit.Role == Role.Heavy ? 1 : 0;

                if (unit.Role == Role.Flamer)
                {
                    unit.Ammo = 3;
                }
                if (unit.Role == Role.WallGuard)
                {
                    //TODO: support WallGuard.
                    throw new NotSupportedException("WallGuard is not supported");
                }
                if (i % 2 == 1 && unit.Role == state.Units[i - 1].Role)
                {
                    throw new ArgumentException("unit's role is duplicated");
                }
            }
            for (int p = 0; p < 4; ++p)
            {
                for (int i = 0; i < 8; ++i)
                {
                    ResetCandidates(p, i);
                }
            }
            snapshots.Add(Deep.CloneSnapshot(state));
        }

        /// <summary>
        /// Construct the constrained view of player.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public View View(int player)
        {
            var view = new View();
            view.RoundDamage = (double[])state.RoundDamage.Clone();
            view.RoundNumber = state.RoundNumber;
            view.RoundMask = state.RoundMask;
            view.Mode = state.Mode;
            view.Map = Deep.CloneMap(state.Map);
            for (int i = 0; i < 81; ++i)
            {
                view.Buildings[i] = Deep.CloneBuilding(state.Buildings[i]);
                view.Fires[i] = Deep.CloneFire(state.Fires[i]);
            }
            view.Rubble = (int[])state.Rubble.Clone();
            view.Roses = new List<Rose>(state.Roses.Count);
            foreach (Rose rose in state.Roses)
            {
                view.Roses.Add(Deep.CloneRose(rose));
            }
            view.Swords = new List<Sword>(state.Swords.Count);
            foreach (Sword sword in state.Swords)
            {
                view.Swords.Add(Deep.CloneSword(sword));
            }
            view.Intel = (bool[])state.Intel[player].Clone();
            view.Units = new Unit[8];
            for (int i = 0; i < 8; ++i)
            {
                view.Units[i] = Deep.CloneUnit(state.Units[i]);
            }
            view.PlayerCount = state.PlayerCount;
            view.Teams = state.Teams;
            view.Eliminated = (bool[])state.Eliminated.Clone();
            view.PlayerTurns = (int[])state.PlayerTurns.Clone();
            view.TeamSize = state.TeamSize;
            view.Player = player;
            view.Turn = state.Turn;
            view.Winner = state.Winner;
            view.Turns = state.Turns;
            view.Revision = state.Revision;
            view.Budget = player != state.Turn ? new Turn() : Deep.CloneTurn(state.Budget);
            view.Possible = ModelArrays.NewMasks(8);
            for (int i = 0; i < 8; ++i)
            {
                view.Possible[i] = (bool[])state.Possible[player][i].Clone();
            }
            // data masking.
            for (int i = 0; i < 8; ++i)
            {
                if (i / 2 == player)
                {
                    continue;
                }
                Unit unit = view.Units[i];
                unit.Position = 0;
                unit.Soldiers = new int[3];
                unit.SoldierSilenced = new bool[3];
                unit.SoldierGuard = new bool[3];
                unit.SoldierWhirl = new bool[3];
                unit.FollowPiece = 0;
                unit.AmmoBlocked = false;
                unit.Silenced = false;
                unit.Guard = false;
                unit.WhirlImmune = false;
                unit.Follow = null;
                unit.Sense = -2;
                unit.Report = string.Empty;
            }
            int? enemyTurn = null;
            for (int i = events.Count - 1; i >= 0; --i)
            {
                Event event_ = events[i];
                if (event_.Player == player)
                {
                    continue;
                }
                if (enemyTurn == null)
                {
                    enemyTurn = event_.Turn;
                }
                if (event_.Turn == enemyTurn)
                {
                    for (int c = 0; c < 81; ++c)
                    {
                        view.EnemyAttacks[c] |= event_.AttackCells[c];
                    }
                }
            }
            view.EnemyAttackTurn = enemyTurn == null ? (int?)null : enemyTurn;
            return view;
        }

        /// <summary>
        /// Commit an action.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public bool Act(GameAction action)
        {
            if (!Validate(View(state.Turn), action, out _))
            {
                return false;
            }
            Event event_ = new Event
            {
                Turn = state.Turns,
                Player = state.Turn,
                Unit = action.Unit,
                Action = Deep.CloneAction(action),
            };
            Turn budget = state.Budget;
            Unit unit = state.Units[action.Unit];
            if (budget.Active == null)
            {
                budget.Active = action.Unit;
            }
            event_.PublicText = unit.Role.ToString() + " · ";
            bool end = action.Kind == ActionKind.End;
            if (action.Kind == ActionKind.Surrender)
            {
                event_.PublicText += "Surrender. ";
                for (int i = state.Turn * 2; i < state.Turn * 2 + 2; ++i)
                {
                    state.Units[i].Health = 0;
                    state.Units[i].SoldierHealth = new double[3];
                }
                CheckVictory();
                if (state.Winner == null)
                {
                    EndTurn();
                }
                Commit(event_);
                return true;
            }
            if (end)
            {
                event_.PublicText += "Turn Ends. ";
            }
            else if (action.Kind == ActionKind.Move)
            {
                MoveUnit(action.Unit, action.Target);
                ++budget.Moves;
                ++budget.Used;
                if (unit.Role == Role.Heavy)
                {
                    budget.Phase = 1;
                }
                event_.PublicText += "Move";
                event_.PrivateText = "Move to " + unit.Position;
            }
            else if (action.Kind == ActionKind.Attack)
            {
                ++budget.Attacks;
                ++budget.Used;
                bool ranged =
                    unit.Role == Role.Sniper
                    || unit.Role == Role.Ink
                    || (unit.Role == Role.HiddenWolf && unit.Form == 0);
                bool execute = GameRules.HeroMode(state.Mode) && unit.Role == Role.Sniper;
                double power = unit.Role == Role.Ink ? 1 : 2;
                event_.AttackCells[action.Target - 1] = true;
                event_.PublicText += "Attack " + action.Target;
                Damage(action.Unit, action.Target, ranged, execute, event_, power);
            }
            CheckVictory();
            if (state.Winner != null)
            {
                end = true;
            }
            if (!end && !HasAction(View(state.Turn), action.Unit))
            {
                end = true;
                event_.PublicText += "\nNo more actions. End turn automatically. ";
            }
            if (end && state.Winner == null)
            {
                EndTurn();
            }
            Commit(event_);
            return true;
        }

        /// <summary>
        /// Commit an event.
        /// </summary>
        /// <param name="event_"></param>
        private void Commit(Event event_)
        {
            ++state.Revision;
            events.Add(event_);
            snapshots.Add(Deep.CloneSnapshot(state));
        }

        /// <summary>
        /// Basic movement.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dest"></param>
        private void MoveUnit(int id, int dest)
        {
            state.Units[id].Position = dest;
            for (int p = 0; p < 4; ++p)
            {
                ResetCandidates(p, id);
            }
        }

        /// <summary>
        /// Damage calculation.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="target"></param>
        /// <param name="ranged"></param>
        /// <param name="execute"></param>
        /// <param name="event_"></param>
        /// <param name="power"></param>
        private void Damage(
            int actor,
            int target,
            bool ranged,
            bool execute,
            Event event_,
            double power
        )
        {
            const double epsilon = 1e-9;
            if (target < 1 || target > MapInfo.CellCount(state.Mode))
            {
                return;
            }
            if (execute)
            {
                power = 1e6;
            }
            bool contact = false;
            for (int j = 0; j < 8; ++j)
            {
                Unit victim = state.Units[j];
                if (victim.Health <= 0 || GameRules.Allied(state.Teams, j / 2, actor / 2))
                {
                    continue;
                }
                if (victim.Position != target)
                {
                    continue;
                }
                contact = true;
                double prior = victim.DirectDamage;
                double delta = execute ? power : GameRules.DirectAttackDelta(prior, power);
                victim.DirectDamage = Math.Max(prior, power);
                if (delta <= epsilon)
                {
                    event_.PublicText +=
                        "\nA direct attack with strength no less than this one has already occurred this round.";
                    continue;
                }
                if (execute)
                {
                    victim.Health = 0;
                    victim.Armor = 0;
                    victim.Shield = 0;
                    event_.PublicText += " Kill (Snipper)";
                    KillCleanup(j);
                }
                double amount = delta;
                if (
                    victim.Role == Role.HiddenWolf
                    && (victim.Form == 0 && ranged || victim.Form != 0 && !ranged)
                )
                {
                    amount /= 2;
                }
                double absorbed = Math.Min(amount, victim.Armor * 2);
                victim.Armor -= absorbed / 2;
                amount -= absorbed;
                double shield = Math.Min(amount, victim.Shield);
                victim.Shield -= shield;
                amount -= shield;
                victim.Health = Math.Max(0, victim.Health - amount);
                event_.PublicText += "\nHit hte target, remaining " + amount;
                if (victim.Health <= epsilon)
                {
                    victim.Health = 0;
                    event_.PublicText += "\nWasted";
                    KillCleanup(j);
                }
            }
            if (!contact)
            {
                event_.PublicText += "\nMissed";
            }
        }

        /// <summary>
        /// Cleanup data after kill.
        /// </summary>
        /// <param name="j"></param>
        private void KillCleanup(int j)
        {
            state.Units[j].Follow = null;
            for (int i = 0; i < 8; ++i)
            {
                if (state.Units[i].Follow == j)
                {
                    state.Units[i].Follow = null;
                }
            }
            for (int p = 0; p < 4; ++p)
            {
                ResetCandidates(p, j);
            }
        }

        /// <summary>
        /// Do some job to end the turn.
        /// </summary>
        /// <param name="event_"></param>
        private void EndTurn()
        {
            int player = state.Turn;
            ++state.PlayerTurns[player];
            for (int i = 0; i < 8; ++i)
            {
                Unit unit = state.Units[i];
                if (i / 2 == player)
                {
                    unit.Cooldown = Math.Max(0, unit.Cooldown - 1);
                    unit.ArmorLock = Math.Max(0, unit.ArmorLock - 1);
                    if (unit.Role == Role.Flamer && unit.Health > 0)
                    {
                        unit.Ammo = Math.Min(3, unit.Ammo + 1);
                        unit.AmmoBlocked = false;
                    }
                    if (unit.Role == Role.Sword && unit.Health > 0)
                    {
                        unit.SwordQi = Math.Min(9, unit.SwordQi + 3);
                    }
                    for (int k = 0; k < 3; ++k)
                    {
                        unit.OptionCooldown[k] = Math.Max(0, unit.OptionCooldown[k] - 1);
                    }
                    if (unit.Role != Role.Shadow)
                    {
                        if (unit.Silenced)
                        {
                            unit.Silenced = false;
                            unit.Guard = true;
                        }
                        else
                        {
                            unit.Guard = false;
                        }
                    }
                }
                else if (unit.Follow != null && unit.Follow / 2 == player)
                {
                    unit.Follow = null;
                }
            }
            ++state.Turns;
            CheckVictory();
            state.Budget = new Turn();
            state.RoundMask |= 1 << player;
            int aliveMask = 0;
            for (int playerIter = 0; playerIter < state.PlayerCount; ++playerIter)
            {
                if (!state.Eliminated[playerIter])
                {
                    aliveMask |= 1 << playerIter;
                }
            }
            if (state.Winner == null && state.Turns >= 40 * state.PlayerCount)
            {
                state.Winner = 4;
            }
            if (state.Winner == null)
            {
                do
                {
                    state.Turn = (state.Turn + 1) % state.PlayerCount;
                } while (state.Eliminated[state.Turn]);
                StartTurn();
                if ((state.RoundMask & aliveMask) == aliveMask)
                {
                    state.RoundMask = 0;
                    ResetDirectDamage();
                    ++state.RoundNumber;
                }
            }
        }

        /// <summary>
        /// Clean flame and shield.
        /// </summary>
        private void StartTurn()
        {
            for (int i = state.Turn * 2; i < state.Turn * 2 + 2; ++i)
            {
                state.Units[i].Shield = 0;
                if (state.Units[i].Role == Role.Shadow)
                {
                    state.Units[i].Guard = state.Units[i].Silenced;
                    state.Units[i].Silenced = false;
                }
            }
        }

        private void CheckVictory()
        {
            int? surviving = null;
            bool opposed = false;
            for (int player = 0; player < state.PlayerCount; ++player)
            {
                bool alive = false;
                for (int i = player * 2; i < player * 2 + 2; ++i)
                {
                    alive |= state.Units[i].Health > 0;
                }
                state.Eliminated[player] = !alive;
                if (alive)
                {
                    if (surviving is null)
                    {
                        surviving = player;
                    }
                    else if (
                        surviving is int survivingPlayer
                        && !GameRules.Allied(state.Teams, survivingPlayer, player)
                    )
                    {
                        opposed = true;
                    }
                }
            }
            state.Winner =
                opposed ? null
                : surviving < 0 ? 4
                : state.Teams ? surviving % 2
                : surviving;
        }

        /// <summary>
        /// Conservatively reset candidates.
        /// </summary>
        /// <param name="observer"></param>
        /// <param name="unit"></param>
        private void ResetCandidates(int observer, int unit)
        {
            for (int cell = 1; cell <= 81; ++cell)
            {
                state.Possible[observer][unit][cell - 1] =
                    state.Units[unit].Health > 0 && MapRules.Valid(state.Mode, cell, state.Map);
            }
        }

        /// <summary>
        /// Reset direct damage.
        /// </summary>
        private void ResetDirectDamage()
        {
            for (int cell = 0; cell < 81; ++cell)
            {
                state.RoundDamage[cell] = 0;
            }
            for (int i = 0; i < 8; ++i)
            {
                state.Units[i].DirectDamage = 0;
                state.Units[i].SoldierDirectDamage = new double[3];
            }
            foreach (Building building in state.Buildings)
            {
                building.DirectDamage = 0;
            }
            foreach (Rose rose in state.Roses)
            {
                rose.DirectDamage = 0;
            }
        }

        /// <summary>
        /// Check if the unit has action.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="unitInter"></param>
        /// <returns></returns>
        private bool HasAction(View view, int unitInter)
        {
            Unit unit = view.Units[unitInter];
            Turn budget = view.Budget;
            switch (unit.Role)
            {
                case Role.Sniper:
                    return budget.Used == 0;
                case Role.Wolf:
                    return budget.Used < 3;
                case Role.Ink:
                    return budget.Moves < 1 || budget.Pellets < 3;
                case Role.Heavy:
                    return budget.Phase != 2 && budget.Moves < 2;
                case Role.HiddenWolf:
                    return unit.Form != 0
                        ? budget.Attacks == 0 || budget.Moves < 2
                        : budget.Used == 0;
                default:
                    return false;
            }
        }

        private bool Validate(View view, GameAction action, out string why)
        {
            why = string.Empty;
            if (
                view.Winner != null
                || view.Turn != view.Player
                || action.Unit < 0
                || action.Unit >= 8
                || action.Unit / 2 != view.Player
                || view.Units[action.Unit].Health <= 0
            )
            {
                why = "Please choose an alive role of yourself. ";
                return false;
            }
            Unit unit = view.Units[action.Unit];
            Turn budget = view.Budget;
            if (budget.Active != null && budget.Active != action.Unit)
            {
                why = "Action has been taken. Don't change the role ";
                return false;
            }
            if (action.Kind == ActionKind.End)
            {
                return true;
            }
            if (action.Kind == ActionKind.Surrender)
            {
                return true;
            }
            bool hero = GameRules.HeroMode(view.Mode);
            if (
                action.Kind
                is ActionKind.Dash
                    or ActionKind.Follow
                    or ActionKind.Lead
                    or ActionKind.Charge
                    or ActionKind.Transform
                    or ActionKind.Hunt
                    or ActionKind.Silence
                    or ActionKind.Cleave
                    or ActionKind.CleaveRush
                    or ActionKind.Retreat
            )
            {
                if (!hero || unit.Silenced)
                {
                    why =
                        "You can't use this action. Silence doesn't stop basic attacking and moving. ";
                    return false;
                }
                //TODO: Complete these actions.
                why = "Wait to undate. ";
                return false;
            }
            if (!hero)
            {
                if (budget.Used != 0)
                {
                    why = "Action has been taken. Please end the turn. ";
                    return false;
                }
                if (action.Kind == ActionKind.Attack)
                {
                    if (
                        MapRules.Distance(view.Mode, unit.Position, action.Target) <= 1
                        && Movement.ClearShot(view.Mode, unit.Position, action.Target, view.Map)
                    )
                    {
                        return true;
                    }
                    why = "You can't attack this target. ";
                    return false;
                }
                //TODO: Wait to update.
                why = "Wait to update. ";
                return false;
            }
            if (action.Kind == ActionKind.Move)
            {
                if (unit.Role == Role.Shadow)
                {
                    //TODO: Wait to update.
                    why = "Wait to update. ";
                    return false;
                }
                if (!Movement.CanMove(view.Mode, unit.Position, action.Target, view.Map))
                {
                    why = "Please select an adjacent walkable tile or your own tile. ";
                    return false;
                }
                bool allowed = unit.Role switch
                {
                    Role.Sniper => budget.Used == 0,
                    Role.Wolf => budget.Used < 3,
                    Role.Ink => budget.Moves < 1,
                    Role.Heavy => budget.Phase != 2 && budget.Moves < 2,
                    Role.HiddenWolf => unit.Form != 0 ? budget.Moves < 2 : budget.Used == 0,
                    _ => false,
                };
                if (allowed)
                {
                    return true;
                }
                why =
                    "You have run out the movement chance, or you can't move with other action taken. ";
                return false;
            }
            if (action.Kind == ActionKind.Attack)
            {
                if (unit.Role == Role.Shadow)
                {
                    //TODO: Wait to update.
                    why = "Wait to update. ";
                    return false;
                }
                if (unit.Role == Role.Heavy)
                {
                    //TODO: Wait ot update.
                    why = "Wait to update. ";
                    return false;
                }
                if (unit.Role == Role.Ink)
                {
                    int stride = MapInfo.Cols(view.Mode);
                    int dRow = Math.Abs(
                        (unit.Position - 1) / stride - (action.Target - 1) / stride
                    );
                    int dCol = Math.Abs(
                        (unit.Position - 1) % stride - (action.Target - 1) % stride
                    );
                    if (
                        Math.Max(dRow, dCol) > 1
                        || !Movement.ClearShot(view.Mode, unit.Position, action.Target, view.Map)
                    )
                    {
                        why = "Target exceeds the range or is blocked by a wall. ";
                        return false;
                    }
                    if (budget.Pellets >= 3)
                    {
                        why = "You cannot attack anymore this turn. ";
                        return false;
                    }
                    return true;
                }
                if (
                    MapRules.Distance(view.Mode, unit.Position, action.Target)
                        > GameRules.AttackRange(view.Mode, unit)
                    || !Movement.ClearShot(view.Mode, unit.Position, action.Target, view.Map)
                )
                {
                    why = "Target exceeds the range or is blocked by a wall. ";
                    return false;
                }
                bool allowed =
                    unit.Role == Role.Wolf ? budget.Used < 3
                    : unit.Role == Role.HiddenWolf
                        ? (
                            unit.Form != 0
                                ? budget.Attacks == 0 && budget.Moves <= 1
                                : budget.Used == 0
                        )
                    : budget.Attacks == 0 && budget.Moves == 0;
                if (allowed)
                {
                    return true;
                }
                why = "You cannot attack on this turn. ";
                return false;
            }
            why = "The current role doesn't support the movement. ";
            return false;
        }
    }

    internal static class Deep
    {
        public static Map CloneMap(Map map)
        {
            var n = new Map();
            n.Width = map.Width;
            n.Height = map.Height;
            n.Cells = (Terrain[])map.Cells.Clone();
            n.HorizontalWalls = (bool[])map.HorizontalWalls.Clone();
            n.VerticalWalls = (bool[])map.VerticalWalls.Clone();
            return n;
        }

        public static GameAction CloneAction(GameAction action)
        {
            return new GameAction
            {
                Unit = action.Unit,
                Kind = action.Kind,
                Target = action.Target,
                Subject = action.Subject,
                Piece = action.Piece,
                Extra = action.Extra,
                Amount = action.Amount,
                Landing = action.Landing,
            };
        }

        public static Building CloneBuilding(Building building)
        {
            return new Building
            {
                Kind = building.Kind,
                Owner = building.Owner,
                Health = building.Health,
                DirectDamage = building.DirectDamage,
            };
        }

        public static Fire CloneFire(Fire fire)
        {
            return new Fire { Owner = fire.Owner, Expires = fire.Expires };
        }

        public static Rose CloneRose(Rose rose)
        {
            return new Rose
            {
                Unit = rose.Unit,
                Cell = rose.Cell,
                Health = rose.Health,
                Id = rose.Id,
                DirectDamage = rose.DirectDamage,
            };
        }

        public static Sword CloneSword(Sword sword)
        {
            return new Sword
            {
                Unit = sword.Unit,
                Cell = sword.Cell,
                Spent = sword.Spent,
            };
        }

        public static Unit CloneUnit(Unit unit)
        {
            return new Unit
            {
                Role = unit.Role,
                Position = unit.Position,
                Form = unit.Form,
                Cooldown = unit.Cooldown,
                Armor = unit.Armor,
                Health = unit.Health,
                Shield = unit.Shield,
                Soldiers = (int[])unit.Soldiers.Clone(),
                SoldierHealth = (double[])unit.SoldierHealth.Clone(),
                Ammo = unit.Ammo,
                ArmorLock = unit.ArmorLock,
                AmmoBlocked = unit.AmmoBlocked,
                OptionCooldown = (int[])unit.OptionCooldown.Clone(),
                Silenced = unit.Silenced,
                Guard = unit.Guard,
                WhirlImmune = unit.WhirlImmune,
                SoldierSilenced = (bool[])unit.SoldierSilenced.Clone(),
                SoldierGuard = (bool[])unit.SoldierGuard.Clone(),
                SoldierWhirl = (bool[])unit.SoldierWhirl.Clone(),
                Follow = unit.Follow,
                Sense = unit.Sense,
                SwordQi = unit.SwordQi,
                Report = unit.Report,
                DirectDamage = unit.DirectDamage,
                SoldierDirectDamage = (double[])unit.SoldierDirectDamage.Clone(),
            };
        }

        public static Turn CloneTurn(Turn turn)
        {
            return new Turn
            {
                Active = turn.Active,
                Moves = turn.Moves,
                Attacks = turn.Attacks,
                Used = turn.Used,
                Phase = turn.Phase,
                Dr = turn.Dr,
                Dc = turn.Dc,
                LastOption = turn.LastOption,
                Options = turn.Options,
                Anchor = turn.Anchor,
                SquadMask = turn.SquadMask,
                SquadDone = turn.SquadDone,
                HitCells = (bool[])turn.HitCells.Clone(),
                FlameCells = (bool[])turn.FlameCells.Clone(),
                FireStep = turn.FireStep,
                Advance = turn.Advance,
                FlameChoice = turn.FlameChoice,
                ShadowMode = turn.ShadowMode,
                ExtraAttacks = turn.ExtraAttacks,
                SwordCells = (bool[])turn.SwordCells.Clone(),
                RoseUsed = new List<int>(turn.RoseUsed),
                SwordAdvance = turn.SwordAdvance,
                SwordBaseUsed = turn.SwordBaseUsed,
                Pellets = turn.Pellets,
                InkDamage = (double[])turn.InkDamage.Clone(),
                Led = turn.Led,
                Followed = turn.Followed,
                Transformed = turn.Transformed,
                Dashed = turn.Dashed,
                DashLast = turn.DashLast,
                Instinct = turn.Instinct,
                ChargeCells = new List<int>(turn.ChargeCells),
            };
        }

        public static Snapshot CloneSnapshot(Snapshot snapshot)
        {
            var n = new Snapshot();
            n.RoundDamage = (double[])snapshot.RoundDamage.Clone();
            n.RoundNumber = snapshot.RoundNumber;
            n.Mode = snapshot.Mode;
            n.Map = snapshot.Map;
            for (int i = 0; i < 81; i++)
            {
                n.Buildings[i] = CloneBuilding(snapshot.Buildings[i]);
                n.Fires[i] = CloneFire(snapshot.Fires[i]);
            }
            n.Rubble = (int[])snapshot.Rubble.Clone();
            n.Roses = new List<Rose>(snapshot.Roses.Count);
            foreach (Rose rose in snapshot.Roses)
            {
                n.Roses.Add(CloneRose(rose));
            }
            n.Swords = new List<Sword>(snapshot.Swords.Count);
            foreach (Sword sword in snapshot.Swords)
            {
                n.Swords.Add(CloneSword(sword));
            }
            for (int player = 0; player < 4; ++player)
            {
                n.Intel[player] = (bool[])snapshot.Intel[player].Clone();
            }
            for (int i = 0; i < 8; ++i)
            {
                n.Units[i] = CloneUnit(snapshot.Units[i]);
            }
            n.PlayerCount = snapshot.PlayerCount;
            n.Teams = snapshot.Teams;
            n.Eliminated = (bool[])snapshot.Eliminated.Clone();
            n.PlayerTurns = (int[])snapshot.PlayerTurns.Clone();
            n.RoundMask = snapshot.RoundMask;
            n.TeamSize = snapshot.TeamSize;
            n.Turn = snapshot.Turn;
            n.Winner = snapshot.Winner;
            n.Turns = snapshot.Turns;
            n.Revision = snapshot.Revision;
            n.Budget = CloneTurn(snapshot.Budget);
            for (int player = 0; player < 4; ++player)
            {
                for (int i = 0; i < 8; ++i)
                {
                    n.Possible[player][i] = (bool[])snapshot.Possible[player][i].Clone();
                }
            }
            return n;
        }
    }
}
