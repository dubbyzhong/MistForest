using System;
using System.Collections.Generic;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IHeavyAction { }

    public class HeavyChargeStart : Action, IHeavyAction
    {
        public Node Target { get; private set; }

        public static HeavyChargeStart New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class HeavyChargeContinue : Action, IHeavyAction
    {
        // The selected endpoint determines the straight direction and distance.
        public Node Target { get; private set; }

        public static HeavyChargeContinue New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    /// <summary>
    /// Dispatches Heavy's general actions and two-stage charge.
    /// </summary>
    public class Heavy : Role
    {
        private readonly Unit[] _units;
        public HeavyUnit Unit => (HeavyUnit)_units[0];

        private Heavy(HeavyUnit unit) => _units = new Unit[] { unit };

        internal static Heavy New(int id, int playerId, HeavyUnit unit)
        {
            Heavy target = new(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Heavy((HeavyUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
        }

        public override bool JudgeAction(Action action, Game game)
        {
            Unit.ClearChargePath();
            if (!Unit.Alive || Unit.End)
            {
                return false;
            }
            if (Unit.IsCharging)
            {
                return action switch
                {
                    HeavyChargeContinue charge => Unit.CanChargeContinue(charge, game),
                    GeneralEnd _ => Unit.CanEnd(),
                    GeneralSurrender _ => Unit.CanSurrender(),
                    _ => false,
                };
            }
            return action switch
            {
                GeneralMove move => Unit.CanMove(move, game),
                HeavyChargeStart charge => Unit.CanChargeStart(charge, game),
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
                case HeavyChargeStart charge:
                    Unit.ChargeStart(charge, game);
                    break;
                case HeavyChargeContinue charge:
                    Unit.ChargeContinue(charge, game);
                    break;
                case GeneralEnd end:
                    Unit.GeneralEnd(end, game);
                    break;
                case GeneralSurrender _:
                    Unit.StopCharge(game);
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

    public class HeavyUnit : Unit
    {
        protected override int MoveDistance => HeavyConsts.HeavyMoveDistance;
        public override int AttackRange => 0;
        private (int dx, int dy)? _chargeDirection;
        private Node[]? _lastChargePath;
        private Node? _lastChargeTarget;
        private Node? _lastChargeFrom;
        public bool IsCharging => Alive && !End && _chargeDirection is not null;

        private HeavyUnit() { }

        public static HeavyUnit New(int id, int roleId, Node position)
        {
            HeavyUnit target = new()
            {
                Health = HeavyConsts.HeavyHealth,
                Armor = HeavyConsts.HeavyInitialArmor,
            };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            // Copy the ongoing charge, but require fresh validation on the clone.
            HeavyUnit target = new() { _chargeDirection = _chargeDirection };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IHeavyAction;

        internal override bool CanMove(GeneralMove move, Game game)
            => !IsCharging && base.CanMove(move, game);

        private bool CanStartCharge => Alive && !End && !IsCharging && MoveUsed == 0 && AttackUsed == 0;

        private static bool CanCrossEdge(Node from, Node to, Game game)
            => game.Board.CanLineShot(from, to, MapConsts.DiagonalBlocked, predicate: _ => true);

        internal Node[] ChargeStartNodes(Game game)
        {
            if (!CanStartCharge)
            {
                return Array.Empty<Node>();
            }
            int playerId = game.PlayerIdFromUnitId(Id);
            List<Node> nodes = new();
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                if (game.Board.Move(Position, dx, dy) is Node next
                    && game.Board.GetGrid(next)!.CanStay(playerId, game)
                    && CanCrossEdge(Position, next, game))
                {
                    nodes.Add(next);
                }
            }
            return nodes.ToArray();
        }

        internal bool CanChargeStart(HeavyChargeStart charge, Game game)
            => Array.IndexOf(ChargeStartNodes(game), charge.Target) >= 0;

        internal void ChargeStart(HeavyChargeStart charge, Game game)
        {
            ClearChargePath();
            Node from = Position;
            _chargeDirection = (
                game.Board.RowOf(charge.Target)!.Value - game.Board.RowOf(from)!.Value,
                game.Board.ColOf(charge.Target)!.Value - game.Board.ColOf(from)!.Value);
            ChangePos(charge.Target, game);
            game.Events.Add(HeavyChargeMoveEvent.New(game.PlayerIdFromUnitId(Id), Id,
                new[] { from, Position }));
        }

        private static bool CanPassBuilding(Building building, int playerId, Game game)
            => building is Building.Fort or Building.GreatWall
                && game.IsPlayerAllied(playerId, building.Owner);

        // A blocking building truncates a valid charge. Terrain and line walls do not
        // consume an action: requests trying to cross them are rejected before execution.
        private Node[] ChargePath(Node target, Game game)
        {
            if (!IsCharging || game.Board.RowOf(target) is not int row
                || game.Board.ColOf(target) is not int col)
            {
                return Array.Empty<Node>();
            }
            int dx = row - game.Board.RowOf(Position)!.Value;
            int dy = col - game.Board.ColOf(Position)!.Value;
            int distance = Math.Abs(dx) + Math.Abs(dy);
            if ((dx != 0 && dy != 0) || distance < 1 || distance >= HeavyConsts.HeavyChargeMaxDistance)
            {
                return Array.Empty<Node>();
            }
            dx = Math.Sign(dx);
            dy = Math.Sign(dy);
            var first = _chargeDirection!.Value;
            if (dx == -first.dx && dy == -first.dy)
            {
                return Array.Empty<Node>();
            }
            int playerId = game.PlayerIdFromUnitId(Id);
            List<Node> path = new();
            Node previous = Position;
            for (int step = 0; step < distance; ++step)
            {
                if (game.Board.Move(previous, dx, dy) is not Node next
                    || game.Board.GetGrid(next)!.Terrain is not (Terrain.Ground or Terrain.Whirlpool)
                    || !CanCrossEdge(previous, next, game))
                {
                    return Array.Empty<Node>();
                }
                path.Add(next);
                Grid grid = game.Board.GetGrid(next)!;
                if (grid.HasBlockingBuilding(game)
                    && !CanPassBuilding(game.Buildings[grid.GridBuilding!.Value], playerId, game))
                {
                    break;
                }
                previous = next;
            }
            return path.ToArray();
        }

        internal void ClearChargePath()
        {
            _lastChargePath = null;
            _lastChargeTarget = null;
            _lastChargeFrom = null;
        }

        internal bool CanChargeContinue(HeavyChargeContinue charge, Game game)
        {
            ClearChargePath();
            Node[] path = ChargePath(charge.Target, game);
            if (path.Length == 0)
            {
                return false;
            }
            _lastChargePath = path;
            _lastChargeTarget = charge.Target;
            _lastChargeFrom = Position;
            return true;
        }

        internal Node[] ChargeContinueNodes(Game game)
        {
            if (!IsCharging)
            {
                return Array.Empty<Node>();
            }
            List<Node> nodes = new();
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                for (int distance = 1; distance < HeavyConsts.HeavyChargeMaxDistance; ++distance)
                {
                    if (game.Board.Move(Position, dx * distance, dy * distance) is not Node target)
                    {
                        break;
                    }
                    Node[] path = ChargePath(target, game);
                    if (path.Length == 0)
                    {
                        break;
                    }
                    nodes.Add(target);
                }
            }
            return nodes.ToArray();
        }

        internal void ChargeContinue(HeavyChargeContinue charge, Game game)
        {
            Node[]? path = _lastChargePath;
            bool validated = IsCharging && _lastChargeTarget == charge.Target
                && _lastChargeFrom == Position && path is not null;
            ClearChargePath();
            if (!validated || path is null)
            {
                throw new InvalidOperationException("Charge continuation must be validated before execution.");
            }
            int playerId = game.PlayerIdFromUnitId(Id);
            List<Node> moved = new() { Position };
            List<Node> hits = new();
            foreach (Node next in path)
            {
                hits.Add(next);
                Grid grid = game.Board.GetGrid(next)!;
                if (grid.HasBlockingBuilding(game))
                {
                    Building building = game.Buildings[grid.GridBuilding!.Value];
                    if (!CanPassBuilding(building, playerId, game))
                    {
                        building.ReceiveDirectDamage(HeavyConsts.HeavyChargeDamage, game, playerId);
                        if (building.Health == 0)
                        {
                            ChangePos(next, game);
                            moved.Add(next);
                        }
                        break;
                    }
                }
                else
                {
                    foreach (int unitId in grid.Units.ToArray())
                    {
                        Unit enemy = game.GetUnit(unitId)!;
                        if (enemy.Alive && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unitId)))
                        {
                            enemy.ReceiveDirectDamage(HeavyConsts.HeavyChargeDamage, game);
                        }
                    }
                }
                ChangePos(next, game);
                moved.Add(next);
            }
            game.Events.Add(HeavyChargeMoveEvent.New(playerId, Id, moved.ToArray()));
            FinishCharge(hits.ToArray(), game);
        }

        private void FinishCharge(Node[] hits, Game game)
        {
            ClearChargePath();
            _chargeDirection = null;
            ++AttackUsed;
            End = true;
            game.Events.Add(HeavyChargeEvent.New(Id, hits));
        }

        internal void StopCharge(Game game)
        {
            ClearChargePath();
            if (IsCharging)
            {
                FinishCharge(Array.Empty<Node>(), game);
            }
        }

        public override void GeneralEnd(GeneralEnd end, Game game)
        {
            StopCharge(game);
            base.GeneralEnd(end, game);
        }

        public override void EndRound(Game game)
        {
            ClearChargePath();
            _chargeDirection = null;
            base.EndRound(game);
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }
            // While choosing the second stage, the owner can always choose End.
            if (IsCharging)
            {
                return true;
            }
            UnitView view = GetView(game);
            return view.CanMoveGrids.Length > 0
                || view.CanBuildFortGrids.Length > 0
                || view.CanBuildWallGrids.Length > 0
                || view.CanDemolishGrids.Length > 0;
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canBuild = active && !IsCharging && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);
            return HeavyUnitView.New(
                canMoveGrids: Box.New(active && !IsCharging && MoveUsed < MoveDistance
                    ? MoveEntries(game)
                    : Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canBuildFortGrids: Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                canBuildWallGrids: Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>()),
                canDemolishGrids: Box.New(DemolishNodes(game)),
                armor: Armor,
                canChargeStartGrids: Box.New(ChargeStartNodes(game)),
                canChargeContinueGrids: Box.New(ChargeContinueNodes(game)),
                isCharging: IsCharging
            )!;
        }
    }

    public class HeavyUnitView : UnitView
    {
        public int Armor { get; private set; }
        public bool IsCharging { get; private set; }
        public Node[] CanChargeStartGrids { get; private set; } = Array.Empty<Node>();
        public Node[] CanChargeContinueGrids { get; private set; } = Array.Empty<Node>();

        private HeavyUnitView() : base(null!, null!, null!, null!) { }

        public static HeavyUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids,
            Box<Node[]> canDemolishGrids,
            int armor,
            Box<Node[]> canChargeStartGrids,
            Box<Node[]> canChargeContinueGrids,
            bool isCharging)
        {
            if (canChargeStartGrids.TryTake() is not Node[] startGrids
                || canChargeContinueGrids.TryTake() is not Node[] continueGrids)
            {
                return null;
            }
            HeavyUnitView target = new()
            {
                Armor = armor,
                IsCharging = isCharging,
                CanChargeStartGrids = startGrids,
                CanChargeContinueGrids = continueGrids,
            };
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids, canDemolishGrids)
                ? target
                : null;
        }

        public override UnitView Clone()
        {
            HeavyUnitView target = new()
            {
                Armor = Armor,
                IsCharging = IsCharging,
                CanChargeStartGrids = (Node[])CanChargeStartGrids.Clone(),
                CanChargeContinueGrids = (Node[])CanChargeContinueGrids.Clone(),
            };
            CloneBase(target);
            return target;
        }
    }

    /// <summary>Public damage-check coordinates, sorted without revealing the travel order.</summary>
    public class HeavyChargeEvent : Event
    {
        public int UnitId { get; }
        public Node[] HitNodes { get; }

        private HeavyChargeEvent(int unitId, Node[] hitNodes)
        {
            UnitId = unitId;
            HitNodes = (Node[])hitNodes.Clone();
            Array.Sort(HitNodes, (left, right) => left.Pos.CompareTo(right.Pos));
        }

        public static HeavyChargeEvent New(int unitId, Node[] hitNodes) => new(unitId, hitNodes);
    }

    /// <summary>Actual movement for one charge stage, visible only to its owner.</summary>
    public class HeavyChargeMoveEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public Node[] Path { get; }

        private HeavyChargeMoveEvent(int playerId, int unitId, Node[] path)
        {
            PlayerId = playerId;
            UnitId = unitId;
            Path = (Node[])path.Clone();
        }

        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;

        public static HeavyChargeMoveEvent New(int playerId, int unitId, Node[] path)
            => new(playerId, unitId, path);
    }

    public class HeavyUnitBuilder : UnitBuilder
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
            Built = HeavyUnit.New(id, roleId, position);
            return Built;
        }
    }

    public class HeavyBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private HeavyBuilder(UnitBuilder[] unitBuilder) : base(unitBuilder) { }

        public static HeavyBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not HeavyUnitBuilder)
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
            if (unitBuilder.Position != Position || unitBuilder.Build() is not HeavyUnit unit)
            {
                return null;
            }
            Built = Heavy.New(id, playerId, unit);
            return Built;
        }
    }
}
