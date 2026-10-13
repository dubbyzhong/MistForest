using System;
using System.Collections.Generic;
using System.Linq;

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
            bool teams,
            int[][] formations = null
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
                if (unit.Role == Role.WallGuard && GameRules.HeroMode(mode))
                {
                    int[] spawn =
                        formations != null && formations[i] != null
                            ? formations[i]
                            : World.GuardSpawn(mode, positions[i], state.Map);
                    if (!World.ValidFormation(mode, state.Map, positions[i], spawn))
                    {
                        throw new ArgumentException("formation is invalid");
                    }
                    unit.Soldiers = (int[])spawn.Clone();
                    unit.SoldierHealth = Enumerable.Repeat(1.6, 3).ToArray();
                    unit.Health = unit.SoldierHealth.Sum();
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
            Whirlpools();
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
                    EndTurn(event_);
                }
                Commit(event_);
                return true;
            }
            if (end)
            {
                event_.PublicText += "Turn Ends. ";
            }
            else if (unit.Role == Role.WallGuard && GameRules.HeroMode(state.Mode))
            {
                GuardAction(action, event_);
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
            else if (action.Kind == ActionKind.FireStep)
            {
                --unit.Ammo;
                budget.FireStep = true;
                MoveUnit(action.Unit, action.Target);
                event_.PublicText +=
                    action.Amount > 0 ? $"Fire step and ignite {action.Target}" : "Fire step";
                event_.PrivateText = $"Fire step to {unit.Position}";
                if (action.Amount > 0)
                {
                    event_.AttackCells[action.Target - 1] = true;
                    unit.AmmoBlocked = true;
                    Ignite(action.Unit, action.Target, event_, true);
                    event_.PrivateText += "; no ammo regenerate this turn. ";
                }
            }
            else if (action.Kind == ActionKind.Demolish)
            {
                DestroyBuilding(action.Target);
                event_.PublicText += $"Demolish the building at {action.Target}, leaving rubble. ";
                end = true;
            }
            else if (action.Kind is ActionKind.BuildFort or ActionKind.BuildWall)
            {
                Build(action, event_);
                end = true;
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
                EndTurn(event_);
            }
            Commit(event_);
            return true;
        }

        /// <summary>
        /// Destroy a building, leaving rubble for two player turns.
        /// </summary>
        /// <param name="cell"></param>
        private void DestroyBuilding(int cell)
        {
            state.Buildings[cell - 1] = new Building();
            state.Rubble[cell - 1] = state.Turns + state.PlayerCount;
        }

        /// <summary>
        /// Build for normal roles: a two-tile wall succeeds or fails as a whole.
        /// Hidden enemies are only judged here - the failure never says why.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="event_"></param>
        private void Build(GameAction action, Event event_)
        {
            Unit unit = state.Units[action.Unit];
            var cells = new List<int> { action.Target };
            if (action.Kind == ActionKind.BuildWall)
            {
                cells.Add(action.Extra);
            }
            bool allowed = true;
            foreach (int cell in cells)
            {
                if (!World.BuildSite(View(state.Turn), unit.Position, cell))
                {
                    allowed = false;
                }
                foreach (Unit other in state.Units)
                {
                    if (other.Health > 0 && other.Position == cell)
                    {
                        allowed = false;
                    }
                }
            }
            event_.PublicText +=
                action.Kind == ActionKind.BuildFort ? "Build a fort" : "Build a wall";
            foreach (int cell in cells)
            {
                event_.PublicText += $" {cell}";
            }
            if (!allowed)
            {
                event_.PublicText += "\nBuild failed, turn ends. ";
                return;
            }
            foreach (int cell in cells)
            {
                state.Buildings[cell - 1] = new Building
                {
                    Kind = action.Kind == ActionKind.BuildFort ? Structure.Fort : Structure.Wall,
                    Owner = action.Unit / 2,
                    Health = 2,
                };
            }
            event_.PublicText += "\nBuild succeeded, turn ends. ";
            unit.Report = "The building is public to both sides. ";
        }

        /// <summary>
        /// Light a cell on fire. The flame lasts until the caster's next turn
        /// starts; burning resolves at the victim-owner's turn end.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="cell"></param>
        /// <param name="event_"></param>
        /// <param name="immediate"></param>
        /// <param name="hitSelf"></param>
        private void Ignite(int actor, int cell, Event event_, bool immediate, bool hitSelf = false)
        {
            if (
                state.Map.Cells[cell - 1] == Terrain.Lake
                || state.Map.Cells[cell - 1] == Terrain.Wall
            )
            {
                return;
            }
            bool onBuilding = state.Buildings[cell - 1].Health > 0;
            if (onBuilding && immediate)
            {
                Damage(actor, cell, true, false, event_, 2, false, null, hitSelf);
            }
            state.Fires[cell - 1].Owner = actor / 2;
            state.Fires[cell - 1].Expires = state.PlayerTurns[actor / 2] + 1;
            if (immediate && !onBuilding)
            {
                Damage(actor, cell, true, false, event_, 2, false, null, hitSelf);
            }
        }

        /// <summary>
        /// Pull every unit toward an adjacent whirlpool at round start.
        /// </summary>
        private void Whirlpools()
        {
            const double epsilon = 1e-9;
            bool anyWhirlpool = false;
            foreach (Terrain terrain in state.Map.Cells)
            {
                if (terrain == Terrain.Whirlpool)
                {
                    anyWhirlpool = true;
                    break;
                }
            }
            if (!anyWhirlpool)
            {
                return;
            }
            for (int i = 0; i < 8; ++i)
            {
                Unit unit = state.Units[i];
                if (unit.Health <= 0)
                {
                    continue;
                }
                if (unit.Role == Role.WallGuard)
                {
                    bool pulled = false;
                    foreach (int k in Enumerable.Range(0, 3))
                    {
                        if (unit.SoldierHealth[k] <= epsilon)
                        {
                            continue;
                        }
                        if (unit.SoldierWhirl[k])
                        {
                            unit.SoldierWhirl[k] = false;
                            continue;
                        }
                        int soldierDest = PullDestination(unit.Soldiers[k]);
                        if (
                            soldierDest != unit.Soldiers[k]
                            && World.CanEnter(View(i / 2), i, soldierDest)
                        )
                        {
                            unit.Soldiers[k] = soldierDest;
                            unit.SoldierWhirl[k] = true;
                            pulled = true;
                        }
                    }
                    RefreshSquad(unit);
                    unit.WhirlImmune = unit.SoldierWhirl[0..3].Any(x => x);
                    if (pulled)
                    {
                        unit.Report =
                            "Soldiers pulled by the whirlpool; each is immune next round. ";
                    }
                    continue;
                }
                if (unit.WhirlImmune)
                {
                    unit.WhirlImmune = false;
                    continue;
                }
                int dest = PullDestination(unit.Position);
                if (dest != unit.Position && World.CanEnter(View(i / 2), i, dest))
                {
                    unit.Report = $"Pulled by the whirlpool to {dest}, immune next round.";
                    unit.Position = dest;
                    unit.WhirlImmune = true;
                }
            }
            for (int player = 0; player < 4; ++player)
            {
                for (int i = 0; i < 8; ++i)
                {
                    bool[] next = new bool[81];
                    for (int cell = 1; cell <= MapInfo.CellCount(state.Mode); ++cell)
                    {
                        if (state.Possible[player][i][cell - 1])
                        {
                            next[cell - 1] = true;
                            next[PullDestination(cell) - 1] = true;
                        }
                    }
                    state.Possible[player][i] = next;
                }
            }
        }

        /// <summary>
        /// Nearest (smallest cell number) adjacent whirlpool reachable by
        /// a terrain move; no chaining.
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        private int PullDestination(int cell)
        {
            for (int cellIter = 1; cellIter <= MapInfo.CellCount(state.Mode); ++cellIter)
            {
                if (
                    state.Map.Cells[cellIter - 1] == Terrain.Whirlpool
                    && MapRules.Distance(state.Mode, cell, cellIter) == 1
                    && Movement.CanMove(state.Mode, cell, cellIter, state.Map)
                )
                {
                    return cellIter;
                }
            }
            return cell;
        }

        private void RefreshSquad(Unit unit)
        {
            if (unit.Role != Role.WallGuard)
            {
                return;
            }
            unit.Health = 0;
            bool chosen = false;
            foreach (int k in Enumerable.Range(0, 3))
            {
                unit.Health += unit.SoldierHealth[k];
                if (unit.SoldierHealth[k] > 0 && !chosen)
                {
                    unit.Position = unit.Soldiers[k];
                    chosen = true;
                }
            }
        }

        /// <summary>
        /// Execute one soldier's action. Each soldier acts at most once per turn.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="event_"></param>
        private void GuardAction(GameAction action, Event event_)
        {
            Unit unit = state.Units[action.Unit];
            Turn budget = state.Budget;
            int from = unit.Soldiers[action.Piece];

            budget.SquadMask = 0;
            foreach (int k in Enumerable.Range(0, 3))
            {
                if (unit.SoldierHealth[k] > 0)
                {
                    budget.SquadMask |= (1 << k);
                }
            }
            budget.SquadDone |= 1 << action.Piece;
            ++budget.Used;

            if (action.Kind == ActionKind.Attack)
            {
                bool onWall = World.GuardWall(state.Buildings[from - 1]);
                event_.AttackCells[action.Target - 1] = true;
                budget.HitCells[action.Target - 1] = true;
                ++budget.Attacks;
                event_.PublicText +=
                    $"Soldier {action.Piece + 1} {(onWall ? "lobs at" : "attacks")} ${action.Target}";
                Damage(action.Unit, action.Target, onWall, false, event_, 2);
                return;
            }

            if (action.Kind == ActionKind.BuildWall)
            {
                int cell = action.Target;
                bool allowed =
                    World.BuildSite(View(state.Turn), from, cell)
                    && (
                        state.Buildings[from - 1].Health <= 0
                        || state.Buildings[from - 1].Kind == Structure.Wall
                    );
                foreach (Unit other in state.Units)
                {
                    if (other.Health <= 0)
                    {
                        continue;
                    }
                    if (other.Role == Role.WallGuard)
                    {
                        foreach (int j in Enumerable.Range(0, 3))
                        {
                            if (other.SoldierHealth[j] > 0 && other.Soldiers[j] == cell)
                            {
                                allowed = false;
                            }
                        }
                    }
                    else if (other.Position == cell)
                    {
                        allowed = false;
                    }
                }
                event_.PublicText +=
                    $"Soldier {action.Piece + 1} builds a great wall at {cell} ${(allowed ? "successfully" : "and fails")}; this soldier's action ends. ";
                if (allowed)
                {
                    state.Buildings[cell - 1] = new Building
                    {
                        Kind = Structure.GreatWall,
                        Owner = action.Unit / 2,
                        Health = 2,
                    };
                }
                return;
            }

            if (action.Kind == ActionKind.Demolish)
            {
                DestroyBuilding(action.Target);
                event_.PublicText +=
                    $"Soldier {action.Piece + 1} demolishes the building at {action.Target}.";
                return;
            }

            if (action.Target != from)
            {
                unit.Soldiers[action.Piece] = action.Target;
            }
            RefreshSquad(unit);
            foreach (int player in Enumerable.Range(0, 4))
            {
                ResetCandidates(player, action.Unit);
            }
            ++budget.Moves;
            event_.PublicText += $"Soldier {action.Target + 1} moves";
            event_.PrivateText += $"from {from} to {unit.Soldiers[action.Piece]}";
        }

        /// <summary>
        /// check whether a build or demolish chance exists.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="unitInter"></param>
        /// <returns></returns>
        private bool HasBuildOrDemolish(View view, int unitInter)
        {
            Unit unit = view.Units[unitInter];
            if (view.Budget.Active != null)
            {
                return false;
            }
            if (view.Buildings[unit.Position - 1].Health > 0)
            {
                return unit.Role != Role.Wolf;
            }
            int width = view.Map.Width;
            int[] deltas = { -width, width, -1, 1 };
            foreach (int delta in deltas)
            {
                int cell = unit.Position + delta;
                if (
                    cell < 1
                    || cell > MapInfo.CellCount(view.Mode)
                    || MapRules.Distance(view.Mode, unit.Position, cell) != 1
                )
                {
                    continue;
                }
                if (view.Buildings[cell - 1].Health > 0)
                {
                    return unit.Role != Role.Wolf;
                }
                if (unit.Role != Role.Wolf && World.BuildSite(view, unit.Position, cell))
                {
                    return true;
                }
            }
            return false;
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
            double power,
            bool burning = false,
            int? burningPlayer = null,
            bool hitSelf = false
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
            if (!burning)
            {
                state.RoundDamage[target - 1] = Math.Max(state.RoundDamage[target - 1], power);
            }
            bool contact = false;
            Building building = state.Buildings[target - 1];
            bool protectedUnits = building.Health > 0 && !execute;
            bool buildingBurns =
                building.Health > 0
                && (!burning || burningPlayer == null || building.Owner == burningPlayer);
            if (buildingBurns)
            {
                contact = true;
                if (
                    !burning
                    && building.Owner != null
                    && GameRules.Allied(state.Teams, (int)building.Owner, actor / 2)
                )
                {
                    event_.PublicText += "\nYour own building is immune to friendly fire. ";
                }
                else if (!burning && state.Units[actor].Role == Role.Wolf)
                {
                    event_.PublicText += "\nThe wolf can't damage buildings. ";
                }
                else if (execute)
                {
                    DestroyBuilding(target);
                    event_.PublicText += "\nSnipper destroyed the building. ";
                }
                else
                {
                    double strength =
                        !burning && state.Units[actor].Role == Role.Ink ? power * 2 : power;
                    double prior = building.DirectDamage;
                    double delta = GameRules.DirectAttackDelta(prior, strength);
                    building.DirectDamage = Math.Max(prior, strength);
                    if (delta > epsilon)
                    {
                        building.Health = Math.Max(0, building.Health - delta);
                        event_.PublicText += $"\nBuilding damaged remaining {building.Health}";
                        if (building.Health <= epsilon)
                        {
                            DestroyBuilding(target);
                        }
                    }
                    else
                    {
                        event_.PublicText +=
                            "\nThe building already took a equal or stronger direct attack this round; no stacking. ";
                    }
                }
            }
            for (int j = 0; j < 8; ++j)
            {
                Unit victim = state.Units[j];
                if (
                    protectedUnits
                    || victim.Health <= 0
                    || burning
                        && burningPlayer is int burningPlayerInner
                        && burningPlayerInner != j / 2
                    || (
                        burning
                            ? victim.Role == Role.Flamer
                            : GameRules.Allied(state.Teams, j / 2, actor / 2)
                                && !(hitSelf && j == actor)
                    )
                )
                {
                    continue;
                }
                int pieces = victim.Role == Role.WallGuard ? 3 : 1;
                bool matched = false;
                foreach (int k in Enumerable.Range(0, pieces))
                {
                    double hp = pieces == 3 ? victim.SoldierHealth[k] : victim.Health;
                    if (hp <= epsilon || World.SoldierCell(victim, k) != target)
                    {
                        continue;
                    }
                    contact = matched = true;
                    string who = pieces == 3 ? $"Guard soldier {k + 1}" : victim.Role.ToString();
                    double prior =
                        pieces == 3 ? victim.SoldierDirectDamage[k] : victim.DirectDamage;
                    double delta =
                        execute ? power
                        : burning ? power
                        : GameRules.DirectAttackDelta(prior, power);
                    if (!burning)
                    {
                        if (pieces == 3)
                        {
                            victim.SoldierDirectDamage[k] = Math.Max(prior, power);
                        }
                        else
                        {
                            victim.DirectDamage = Math.Max(prior, power);
                        }
                    }
                    if (delta <= epsilon)
                    {
                        event_.PublicText +=
                            $"\n{who}: a direct attack with strength no less than this one has already occurred this round.";
                        continue;
                    }
                    if (execute)
                    {
                        if (pieces == 3)
                        {
                            victim.SoldierHealth[k] = 0;
                        }
                        else
                        {
                            victim.Health = 0;
                        }
                        victim.Armor = 0;
                        victim.Shield = 0;
                        event_.PublicText += $"\nKill {who} (Sniper)";
                        continue;
                    }
                    double amount = delta;
                    if (
                        victim.Role == Role.HiddenWolf
                        && !burning
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
                    bool shared = false;
                    if (!burning && pieces == 3 && amount > 0)
                    {
                        int mask = World.LinkedGuards(state.Mode, victim, k);
                        int count = 0;
                        int deaths = 0;
                        foreach (int g in Enumerable.Range(0, 3))
                        {
                            if ((mask & (1 << g)) != 0)
                            {
                                count++;
                            }
                        }
                        double each = amount / count;
                        foreach (int g in Enumerable.Range(0, 3))
                        {
                            if ((mask & (1 << g)) != 0 && victim.SoldierHealth[g] <= each + epsilon)
                            {
                                ++deaths;
                            }
                        }
                        int direct = hp <= amount + epsilon ? 1 : 0;
                        if (count > 1 && deaths <= direct)
                        {
                            shared = true;
                            foreach (int g in Enumerable.Range(0, 3))
                            {
                                if ((mask & (1 << g)) != 0)
                                {
                                    victim.SoldierHealth[g] = Math.Max(
                                        0,
                                        victim.SoldierHealth[g] - each
                                    );
                                }
                                event_.PublicText += $"\nSquad shares the damage: {each} each. ";
                            }
                        }
                    }
                    if (shared)
                    {
                        hp = pieces == 3 ? victim.SoldierHealth[k] : victim.Health;
                    }
                    else
                    {
                        hp = Math.Max(0, hp - amount);
                        if (hp < epsilon)
                        {
                            hp = 0;
                        }
                        if (pieces == 3)
                        {
                            victim.SoldierHealth[k] = hp;
                        }
                        else
                        {
                            victim.Health = hp;
                        }
                        event_.PublicText +=
                            hp > 0 ? $"\n{who} hit, remaining {hp}" : $"\n{who} dies.";
                    }
                    RefreshSquad(victim);
                    if (!burning && pieces != 3)
                    {
                        if (matched)
                        {
                            foreach (int cell in Enumerable.Range(0, 81))
                            {
                                state.Possible[actor / 2][j][cell] = false;
                            }
                            state.Possible[actor / 2][j][target - 1] = victim.Health > 0;
                        }
                        else
                        {
                            state.Possible[actor / 2][j][target - 1] = false;
                        }
                    }
                    if (victim.Health <= epsilon)
                    {
                        KillCleanup(j);
                    }
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
        private void EndTurn(Event event_)
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
                        if (unit.AmmoBlocked)
                        {
                            event_.PublicText += "\nIgnited this turn: no ammo regenerate. ";
                        }
                        else
                        {
                            unit.Ammo = Math.Min(
                                3,
                                unit.Ammo + (state.Fires[unit.Position - 1].Expires != 0 ? 2 : 1)
                            );
                        }
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
                    foreach (int k in Enumerable.Range(0, 3))
                    {
                        if (unit.SoldierSilenced[k])
                        {
                            unit.SoldierSilenced[k] = false;
                            unit.SoldierGuard[k] = true;
                        }
                        else
                        {
                            unit.SoldierGuard[k] = false;
                        }
                    }
                }
                else if (unit.Follow != null && unit.Follow / 2 == player)
                {
                    unit.Follow = null;
                }
            }
            for (int cell = 1; cell <= MapInfo.CellCount(state.Mode); ++cell)
            {
                if (state.Fires[cell - 1].Expires != 0)
                {
                    event_.PublicText += $"\nBurning resolves at {cell}";
                    Damage(
                        (state.Fires[cell - 1].Owner ?? 0) * 2,
                        cell,
                        true,
                        false,
                        event_,
                        2,
                        true,
                        player
                    );
                }
            }
            ++state.Turns;
            for (int cell = 0; cell < MapInfo.CellCount(state.Mode); ++cell)
            {
                if (state.Rubble[cell] != 0 && state.Rubble[cell] <= state.Turns)
                {
                    state.Rubble[cell] = 0;
                }
            }
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
                StartTurn(state.Mode);
                if ((state.RoundMask & aliveMask) == aliveMask)
                {
                    state.RoundMask = 0;
                    ResetDirectDamage();
                    ++state.RoundNumber;
                    Whirlpools();
                }
            }
        }

        /// <summary>
        /// Clean flame and shield.
        /// </summary>
        private void StartTurn(Mode mode)
        {
            for (int cell = 0; cell < MapInfo.CellCount(mode); ++cell)
            {
                Fire fire = state.Fires[cell];
                if (
                    fire.Expires != 0
                    && fire.Owner == state.Turn
                    && state.PlayerTurns[state.Turn] >= fire.Expires
                )
                {
                    state.Fires[cell] = new Fire();
                }
            }
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

        /// <summary>
        /// Check if someone gains victory.
        /// </summary>
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
            if (GameRules.HeroMode(view.Mode) && HasBuildOrDemolish(view, unitInter))
            {
                return true;
            }
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
                case Role.Flamer:
                    return budget.Moves < 1 || (!budget.FireStep && unit.Ammo > 0);
                case Role.WallGuard:
                    foreach (int k in Enumerable.Range(0, 3))
                    {
                        if (unit.SoldierHealth[k] > 0 && (budget.SquadDone & (1 << k)) == 0)
                        {
                            return true;
                        }
                    }
                    return false;
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
                || action.Piece < 0
                || action.Piece > 2
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
            if (unit.Role == Role.WallGuard && hero)
            {
                if (unit.SoldierHealth[action.Piece] <= 0)
                {
                    why = "Please choose an alive soldier. ";
                    return false;
                }
                int from = World.SoldierCell(unit, action.Piece);
                bool soldierFresh = (budget.SquadDone & (1 << action.Piece)) == 0;
                if (!soldierFresh && action.Kind != ActionKind.End)
                {
                    why = "This soldier has already acted this turn. ";
                    return false;
                }
                if (action.Kind == ActionKind.Move)
                {
                    if (World.WorldMove(view, action.Unit, from, action.Target))
                    {
                        return true;
                    }
                    why = "This soldier can't move to this tile. ";
                    return false;
                }
                if (action.Kind == ActionKind.Attack)
                {
                    if (action.Target < 1 || action.Target > MapInfo.CellCount(view.Mode))
                    {
                        why = "Target out of range. ";
                        return false;
                    }
                    int range = 1;
                    if (World.GuardWall(view.Buildings[from - 1]))
                    {
                        range = 2;
                    }
                    if (
                        MapRules.Distance(view.Mode, from, action.Target) <= range
                        && World.WorldShot(view, from, action.Target)
                    )
                    {
                        return true;
                    }
                    why = "This soldier can't attack this tile. ";
                    return false;
                }
                if (action.Kind == ActionKind.Demolish)
                {
                    if (action.Target < 1 || action.Target > MapInfo.CellCount(view.Mode))
                    {
                        why = "Target out of range. ";
                        return false;
                    }
                    if (
                        MapRules.Distance(view.Mode, from, action.Target) <= 1
                        && view.Buildings[action.Target - 1].Health > 0
                        && GameRules.Allied(
                            view.Teams,
                            view.Player,
                            (int)view.Buildings[action.Target - 1].Owner
                        )
                    )
                    {
                        return true;
                    }
                    why = "This soldier can't demolish this building. ";
                    return false;
                }
                if (action.Kind == ActionKind.BuildFort)
                {
                    why = "The guard can only build one-tile great walls. ";
                    return false;
                }
                if (action.Kind == ActionKind.BuildWall)
                {
                    bool standingOk =
                        view.Buildings[from - 1].Health <= 0
                        || view.Buildings[from - 1].Kind == Structure.Wall;
                    if (
                        standingOk
                        && action.Target >= 1
                        && action.Target <= MapInfo.CellCount(view.Mode)
                        && action.Target != from
                        && World.BuildReach(view, from, action.Target)
                    )
                    {
                        return true;
                    }
                    why = "Choose an adjacent empty tile for the great wall. ";
                    return false;
                }
                why = "Choose this soldier's move, attack, build or demolish. ";
                return false;
            }
            bool fresh = budget.Active == null;
            if (action.Kind == ActionKind.Demolish)
            {
                bool pass =
                    hero
                    && unit.Role != Role.Wolf
                    && fresh
                    && action.Target >= 1
                    && action.Target <= MapInfo.CellCount(view.Mode)
                    && view.Buildings[action.Target - 1].Health > 0
                    && MapRules.Distance(view.Mode, unit.Position, action.Target) <= 1
                    && World.BuildReach(view, unit.Position, action.Target)
                    && GameRules.Allied(
                        view.Teams,
                        view.Player,
                        (int)view.Buildings[action.Target - 1].Owner
                    );
                if (pass)
                {
                    return true;
                }
                why =
                    "Demolishing costs a full action; the target is a reachable building within distance 1. ";
                return false;
            }
            if (action.Kind is ActionKind.BuildFort or ActionKind.BuildWall)
            {
                if (!hero || !fresh || view.Buildings[unit.Position - 1].Health > 0)
                {
                    why =
                        "Building must be the first action, and you can't build while standing in a building. ";
                    return false;
                }
                if (
                    action.Target < 1
                    || action.Target > MapInfo.CellCount(view.Mode)
                    || action.Target == unit.Position
                )
                {
                    why = "You can't build on this tile. ";
                    return false;
                }
                if (action.Kind == ActionKind.BuildFort)
                {
                    if (
                        MapRules.Distance(view.Mode, unit.Position, action.Target) == 1
                        && World.BuildSite(view, unit.Position, action.Target)
                    )
                    {
                        return true;
                    }
                    why = "Please select one of the four adjacent tiles. ";
                    return false;
                }
                int width = view.Map.Width;
                bool near(int cell) =>
                    cell >= 1
                    && cell <= MapInfo.CellCount(view.Mode)
                    && cell != unit.Position
                    && Math.Abs((cell - 1) / width - (unit.Position - 1) / width) <= 1
                    && Math.Abs((cell - 1) % width - (unit.Position - 1) % width) <= 1;
                if (
                    near(action.Target)
                    && near(action.Extra)
                    && MapRules.Distance(view.Mode, action.Target, action.Extra) == 1
                    && World.BuildSite(view, unit.Position, action.Target)
                    && World.BuildSite(view, unit.Position, action.Extra)
                )
                {
                    return true;
                }
                why =
                    "The wall needs two adjacent tiles in the 3x3 area around you, not the covering yourself. ";
                return false;
            }
            if (action.Kind == ActionKind.FireStep)
            {
                bool pass =
                    unit.Role == Role.Flamer
                    && !budget.FireStep
                    && unit.Ammo > 0
                    && (action.Amount is 0 or 1)
                    && MapRules.Distance(view.Mode, unit.Position, action.Target) == 1
                    && World.WorldMove(view, action.Unit, unit.Position, action.Target);
                if (pass)
                {
                    return true;
                }
                why = "Fire step: once per turn, costs 1 ammo, to an adjacent reachable tile. ";
                return false;
            }
            bool moving =
                action.Kind
                is ActionKind.Move
                    or ActionKind.Lead
                    or ActionKind.Dash
                    or ActionKind.Leap
                    or ActionKind.Hunt
                    or ActionKind.Retreat
                    or ActionKind.Charge
                    or ActionKind.CleaveRush;
            for (int cell = 1; cell <= 81; ++cell)
            {
                if (
                    view.Buildings[cell - 1].Health > 0
                    && cell != unit.Position
                    && (moving ? !World.CanEnter(view, action.Unit, cell) : cell != action.Target)
                )
                {
                    view.Map.Cells[cell - 1] = Terrain.Wall;
                }
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
                    Role.Flamer => budget.Attacks == 0 && budget.Moves < 1,
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
