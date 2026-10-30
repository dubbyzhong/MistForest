using System;
using System.Collections.Generic;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    /// <summary>
    /// Marks actions supported by Armiger.
    /// </summary>
    public interface IArmigerAction { }

    /// <summary>
    /// Requests a straight thrust in the direction of an adjacent cardinal grid.
    /// </summary>
    public class ArmigerThrust : Action, IArmigerAction
    {
        public Node Target { get; private set; }

        public static ArmigerThrust New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    /// <summary>
    /// Records all grids, units, and the blocking building targeted by one thrust.
    /// </summary>
    public class ArmigerThrustEvent : AttackEvent
    {
        public Node[] HitNodes { get; }

        private ArmigerThrustEvent(int attackerId, int[] unitIds, int? buildingId, Node target, Node[] hitNodes)
            : base(attackerId, unitIds, buildingId, target)
        {
            HitNodes = (Node[])hitNodes.Clone();
        }

        public static ArmigerThrustEvent New(int attackerId, int[] unitIds, int? buildingId,
            Node target, Node[] hitNodes)
            => new(attackerId, unitIds, buildingId, target, hitNodes);
    }

    /// <summary>
    /// Requests armor restoration to the next whole layer and ends the turn.
    /// </summary>
    public class ArmigerArmorUp : Action, IArmigerAction
    {
        public static ArmigerArmorUp New(int callerUnitId) => new() { CallerUnitId = callerUnitId };
    }

    /// <summary>
    /// Requests armor removal in half-point units, including any remaining partial layer.
    /// </summary>
    public class ArmigerArmorDown : Action, IArmigerAction
    {
        public int Amount { get; private set; }

        public static ArmigerArmorDown New(int callerUnitId, int amount)
            => new() { CallerUnitId = callerUnitId, Amount = amount };
    }

    /// <summary>
    /// Records armor before and after restoration in half-point units.
    /// </summary>
    public class ArmigerArmorUpEvent : Event
    {
        public int UnitId { get; }
        public int ArmorBefore { get; }
        public int ArmorAfter { get; }

        private ArmigerArmorUpEvent(int unitId, int armorBefore, int armorAfter)
        {
            UnitId = unitId;
            ArmorBefore = armorBefore;
            ArmorAfter = armorAfter;
        }

        public static ArmigerArmorUpEvent New(int unitId, int armorBefore, int armorAfter)
            => new(unitId, armorBefore, armorAfter);
    }

    /// <summary>
    /// Records armor before and after unloading in half-point units.
    /// </summary>
    public class ArmigerArmorDownEvent : Event
    {
        public int UnitId { get; }
        public int ArmorBefore { get; }
        public int ArmorAfter { get; }

        private ArmigerArmorDownEvent(int unitId, int armorBefore, int armorAfter)
        {
            UnitId = unitId;
            ArmorBefore = armorBefore;
            ArmorAfter = armorAfter;
        }

        public static ArmigerArmorDownEvent New(int unitId, int armorBefore, int armorAfter)
            => new(unitId, armorBefore, armorAfter);
    }

    /// <summary>
    /// Dispatches Armiger's actions to its unit.
    /// </summary>
    public class Armiger : Role
    {
        private readonly Unit[] _units;
        public ArmigerUnit Unit => (ArmigerUnit)_units[0];

        private Armiger(ArmigerUnit unit) => _units = new Unit[] { unit };

        internal static Armiger New(int id, int playerId, ArmigerUnit unit)
        {
            Armiger target = new(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Armiger((ArmigerUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
        }

        public override bool JudgeAction(Action action, Game game)
        {
            if (!Unit.Alive || Unit.End)
            {
                return false;
            }
            // TODO: Integrate skill availability with the shared silence system when it is migrated.
            return action switch
            {
                GeneralMove move => Unit.CanMove(move, game),
                ArmigerThrust thrust => Unit.CanThrust(thrust, game),
                ArmigerArmorUp _ => Unit.CanArmorUp(),
                ArmigerArmorDown armorDown => Unit.CanArmorDown(armorDown),
                GeneralEnd _ => Unit.CanEnd(),
                GeneralSurrender _ => Unit.CanSurrender(),
                GeneralBuildFort buildFort => Unit.CanBuildFort(buildFort, game),
                GeneralBuildWall buildWall => Unit.CanBuildWall(buildWall, game),
                GeneralDemolish demolish => Unit.CanDemolish(demolish, game),
                _ => false,
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            Unit.StartExecuteAction();
            switch (action)
            {
                case GeneralMove move:
                    Unit.GeneralMove(move, game);
                    break;
                case ArmigerThrust thrust:
                    Unit.Thrust(thrust, game);
                    break;
                case ArmigerArmorUp _:
                    Unit.ArmorUp(game);
                    break;
                case ArmigerArmorDown armorDown:
                    Unit.ArmorDown(armorDown, game);
                    break;
                case GeneralEnd end:
                    Unit.GeneralEnd(end, game);
                    break;
                case GeneralSurrender _:
                    GeneralSurrender(game);
                    break;
                case GeneralBuildFort buildFort:
                    Unit.GeneralBuildFort(buildFort, game);
                    break;
                case GeneralBuildWall buildWall:
                    Unit.GeneralBuildWall(buildWall, game);
                    break;
                case GeneralDemolish demolish:
                    Unit.GeneralDemolish(demolish, game);
                    break;
            }
        }

        public override bool HasActionLeft(Game game) => !End(game);

        public override ReadOnlySpan<Unit> GetUnits() => _units;
    }

    /// <summary>
    /// Stores Armiger's state and executes general actions, armor skills, and thrusts.
    /// </summary>
    public class ArmigerUnit : Unit
    {
        private int ArmorLevel => (Armor + ArmigerConsts.ArmigerArmorPerLayer - 1)
            / ArmigerConsts.ArmigerArmorPerLayer;
        protected override int MoveDistance => ArmigerConsts.ArmigerMoveDistance - ArmorLevel;
        public override int AttackRange => ArmigerConsts.ArmigerThrustBaseRange + ArmorLevel;
        public int ThrustDamage => ArmigerConsts.ArmigerThrustDamage;
        public int ArmorLockRounds { get; private set; }
        private bool _skipArmorLockTickThisRound;
        private Node[]? _lastThrustPath;

        private ArmigerUnit() { }

        public static ArmigerUnit New(int id, int roleId, Node position)
        {
            ArmigerUnit target = new() { Health = ArmigerConsts.ArmigerHealth };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            ArmigerUnit target = new()
            {
                ArmorLockRounds = ArmorLockRounds,
                _skipArmorLockTickThisRound = _skipArmorLockTickThisRound,
            };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IArmigerAction;

        internal override bool CanMove(GeneralMove move, Game game)
            => AttackUsed == 0 && base.CanMove(move, game);

        private bool CanThrustGeneral() => Alive && !End && AttackUsed == 0;

        /// <summary>
        /// Returns selectable adjacent grids that determine the direction of a thrust.
        /// </summary>
        internal Node[] ThrustNodes(Game game)
        {
            List<Node> targets = new();
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                if (game.Board.Move(Position, dx, dy) is Node target && ThrustPath(target, game).Length > 0)
                {
                    targets.Add(target);
                }
            }
            return targets.ToArray();
        }

        /// <summary>
        /// Returns the straight attack path, including the first standing building and stopping there.
        /// </summary>
        private Node[] ThrustPath(Node first, Game game)
        {
            if (game.Board.Distance(Position, first) != 1)
            {
                return Array.Empty<Node>();
            }
            int dx = game.Board.RowOf(first)!.Value - game.Board.RowOf(Position)!.Value;
            int dy = game.Board.ColOf(first)!.Value - game.Board.ColOf(Position)!.Value;
            List<Node> path = new();
            Node previous = Position;
            for (int step = 0; step < AttackRange; ++step)
            {
                if (game.Board.Move(previous, dx, dy) is not Node next)
                {
                    break;
                }
                Grid grid = game.Board.GetGrid(next)!;
                if (grid.Terrain is Terrain.Wall
                    || !game.Board.CanLineShot(previous, next, MapConsts.DiagonalBlocked,
                        predicate: node => game.Board.GetGrid(node)!.CanShot(game)))
                {
                    break;
                }
                path.Add(next);
                if (grid.HasBlockingBuilding(game))
                {
                    break;
                }
                previous = next;
            }
            return path.ToArray();
        }

        internal bool CanThrust(ArmigerThrust action, Game game)
        {
            _lastThrustPath = null;
            if (!CanThrustGeneral())
            {
                return false;
            }
            Node[] path = ThrustPath(action.Target, game);
            if (path.Length == 0)
            {
                return false;
            }
            _lastThrustPath = path;
            return true;
        }

        internal void Thrust(ArmigerThrust action, Game game)
        {
            ++AttackUsed;
            End = true;
            Node[] path = _lastThrustPath!;
            int playerId = game.PlayerIdFromUnitId(Id);
            List<int> attackedUnits = new();
            int? attackedBuildingId = null;
            foreach (Node node in path)
            {
                Grid grid = game.Board.GetGrid(node)!;
                if (grid.HasBlockingBuilding(game))
                {
                    Building building = game.Buildings[grid.GridBuilding!.Value];
                    attackedBuildingId = building.Id;
                    building.ReceiveDirectDamage(ThrustDamage, game, playerId);
                }
                else
                {
                    foreach (int unitId in grid.Units.ToArray())
                    {
                        Unit enemy = game.GetUnit(unitId)!;
                        if (enemy.Alive && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unitId)))
                        {
                            enemy.ReceiveDirectDamage(ThrustDamage, game);
                            attackedUnits.Add(unitId);
                        }
                    }
                }
            }
            _lastThrustPath = null;
            game.Events.Add(ArmigerThrustEvent.New(Id, attackedUnits.ToArray(), attackedBuildingId,
                action.Target, path));
        }

        internal bool CanArmorUp()
            => Alive && !End && IsFirstAction && ArmorLockRounds == 0
                && Armor < ArmigerConsts.ArmigerMaxArmor;

        private bool CanArmorDownGeneral() => Alive && !End && AttackUsed == 0 && Armor > 0;

        internal bool CanArmorDown(ArmigerArmorDown action)
            => CanArmorDownGeneral() && action.Amount > 0 && action.Amount <= Armor
                && (Armor - action.Amount) % ArmigerConsts.ArmigerArmorPerLayer == 0;

        private int[] ArmorDownAmounts()
        {
            if (!CanArmorDownGeneral())
            {
                return Array.Empty<int>();
            }
            int first = Armor % ArmigerConsts.ArmigerArmorPerLayer;
            if (first == 0)
            {
                first = ArmigerConsts.ArmigerArmorPerLayer;
            }
            int[] amounts = new int[(Armor - first) / ArmigerConsts.ArmigerArmorPerLayer + 1];
            for (int i = 0; i < amounts.Length; ++i)
            {
                amounts[i] = first + i * ArmigerConsts.ArmigerArmorPerLayer;
            }
            return amounts;
        }

        internal void ArmorUp(Game game)
        {
            int before = Armor;
            Armor = (Armor / ArmigerConsts.ArmigerArmorPerLayer + 1) * ArmigerConsts.ArmigerArmorPerLayer;
            End = true;
            game.Events.Add(ArmigerArmorUpEvent.New(Id, before, Armor));
        }

        internal void ArmorDown(ArmigerArmorDown action, Game game)
        {
            int before = Armor;
            Armor -= action.Amount;
            game.Events.Add(ArmigerArmorDownEvent.New(Id, before, Armor));
        }

        internal override DamageResult ReceiveDirectDamage(int strength, Game game)
        {
            DamageResult result = base.ReceiveDirectDamage(strength, game);
            if (result.TotalDamage > 0)
            {
                int playerId = game.PlayerIdFromUnitId(Id);
                ArmorLockRounds = ArmigerConsts.ArmigerArmorLockRounds;
                _skipArmorLockTickThisRound = game.CurrentPlayerId == playerId
                    || game.HasPlayerActedThisRound(playerId);
            }
            return result;
        }

        public override void EndRound(Game game)
        {
            base.EndRound(game);
            if (!_skipArmorLockTickThisRound && ArmorLockRounds > 0)
            {
                --ArmorLockRounds;
            }
            _skipArmorLockTickThisRound = false;
            _lastThrustPath = null;
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }
            ArmigerUnitView view = (ArmigerUnitView)GetView(game);
            return view.CanArmorUp || view.CanArmorDownAmounts.Length > 0
                || view.CanThrustGrids.Length > 0
                || view.CanMoveGrids.Length > 0
                || view.CanBuildFortGrids.Length > 0
                || view.CanBuildWallGrids.Length > 0
                || view.CanDemolishGrids.Length > 0;
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canBuild = active && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);
            return ArmigerUnitView.New(
                canMoveGrids: Box.New(active && AttackUsed == 0 && MoveUsed < MoveDistance
                    ? MoveEntries(game)
                    : Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canBuildFortGrids: Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                canBuildWallGrids: Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>()),
                canDemolishGrids: Box.New(DemolishNodes(game)),
                armor: Armor,
                armorLockRounds: ArmorLockRounds,
                canArmorUp: CanArmorUp(),
                canArmorDownAmounts: Box.New(ArmorDownAmounts()),
                canThrustGrids: Box.New(CanThrustGeneral() ? ThrustNodes(game) : Array.Empty<Node>())
            )!;
        }
    }

    /// <summary>
    /// Exposes available actions and an armor snapshot in half-point units.
    /// </summary>
    public class ArmigerUnitView : UnitView
    {
        public int Armor { get; private set; }
        public int ArmorLockRounds { get; private set; }
        public bool CanArmorUp { get; private set; }
        public int[] CanArmorDownAmounts { get; private set; }
        public Node[] CanThrustGrids { get; private set; }

        private ArmigerUnitView(int[] canArmorDownAmounts, Node[] canThrustGrids) : base(null!, null!, null!, null!)
        {
            CanArmorDownAmounts = canArmorDownAmounts;
            CanThrustGrids = canThrustGrids;
        }

        public static ArmigerUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids,
            Box<Node[]> canDemolishGrids,
            int armor,
            int armorLockRounds,
            bool canArmorUp,
            Box<int[]> canArmorDownAmounts,
            Box<Node[]> canThrustGrids)
        {
            if (canArmorDownAmounts.TryTake() is not int[] downAmounts)
            {
                return null;
            }
            if (canThrustGrids.TryTake() is not Node[] thrustGrids)
            {
                return null;
            }
            ArmigerUnitView target = new(downAmounts, thrustGrids)
            {
                Armor = armor,
                ArmorLockRounds = armorLockRounds,
                CanArmorUp = canArmorUp,
            };
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids, canDemolishGrids)
                ? target
                : null;
        }

        public override UnitView Clone()
        {
            ArmigerUnitView target = new((int[])CanArmorDownAmounts.Clone(), (Node[])CanThrustGrids.Clone())
            {
                Armor = Armor,
                ArmorLockRounds = ArmorLockRounds,
                CanArmorUp = CanArmorUp,
            };
            CloneBase(target);
            return target;
        }
    }

    /// <summary>
    /// Builds an Armiger unit from its identifiers and starting position.
    /// </summary>
    public class ArmigerUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is not null)
            {
                return Built;
            }
            if (Id is not int id || RoleId is not int roleId || Position is not Node position)
            {
                return null;
            }
            Built = ArmigerUnit.New(id, roleId, position);
            return Built;
        }
    }

    /// <summary>
    /// Builds a role containing one Armiger unit.
    /// </summary>
    public class ArmigerBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private ArmigerBuilder(UnitBuilder[] unitBuilder) : base(unitBuilder) { }

        public static ArmigerBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not ArmigerUnitBuilder)
            {
                return null;
            }
            return new(unitBuilder) { Position = position };
        }

        public override bool CanInitialStay(Board board)
        {
            return board.GetGrid(Position) is Grid grid && grid.CanInitialStay();
        }

        public override Role? Build()
        {
            if (Built is not null)
            {
                return Built;
            }
            if (Id is not int id || PlayerId is not int playerId)
            {
                return null;
            }
            UnitBuilder unitBuilder = UnitBuilder[0];
            if (unitBuilder.Position is null)
            {
                unitBuilder.SetPosition(Position);
            }
            if (unitBuilder.Position != Position || unitBuilder.Build() is not ArmigerUnit unit)
            {
                return null;
            }
            Built = Armiger.New(id, playerId, unit);
            return Built;
        }
    }
}
