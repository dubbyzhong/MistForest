using System;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using ZLinq;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    /// <summary>
    /// Marks actions supported by Ink.
    /// </summary>
    public interface IInkAction { }

    /// <summary>
    /// Requests one pellet attack against a grid.
    /// </summary>
    public class InkAttack : Action, IInkAction
    {
        public Node Target { get; private set; }

        public static InkAttack New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    /// <summary>
    /// Requests a two-step dash without spending ordinary movement or pellet attacks.
    /// </summary>
    public class InkDash : Action, IInkAction
    {
        public Node Target { get; private set; }

        public static InkDash New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    /// <summary>
    /// Records the units and building targeted by one pellet attack.
    /// </summary>
    public class InkAttackEvent : AttackEvent
    {
        private InkAttackEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static InkAttackEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
        {
            return new(attackerId, unitIds, buildingId, target);
        }
    }

    /// <summary>
    /// Dispatches Ink's actions to its unit.
    /// </summary>
    public class Ink : Role
    {
        private readonly Unit[] _units;
        public InkUnit Unit => (InkUnit)_units[0];

        private Ink(InkUnit unit) => _units = new Unit[] { unit };

        internal static Ink New(int id, int playerId, InkUnit unit)
        {
            Ink target = new(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Ink((InkUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
        }

        public override bool JudgeAction(Action action, Game game)
        {
            if (!Unit.Alive || Unit.End)
            {
                return false;
            }
            return action switch
            {
                GeneralMove move => Unit.CanMove(move, game),
                InkAttack attack => Unit.CanAttack(attack, game),
                InkDash dash => Unit.CanDash(dash, game),
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
                case InkAttack attack:
                    Unit.InkAttack(attack, game);
                    break;
                case InkDash _:
                    Unit.InkDash(game);
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
    /// Stores Ink's state and executes movement, pellet attacks, and dashes.
    /// </summary>
    public class InkUnit : Unit
    {
        protected override int MoveDistance => InkConsts.InkMoveDistance;
        public override int AttackRange => InkConsts.InkAttackRange;
        public int MaxAttackAction => InkConsts.InkMaxAttackAction;
        public int AttackDamage => InkConsts.InkAttackDamage;
        public int BuildingAttackDamage => InkConsts.InkBuildingAttackDamage;
        public int DashCooldown { get; private set; }
        private bool _dashUsedThisRound;
        private Node[]? _lastDashPath;

        private InkUnit() { }

        public static InkUnit New(int id, int roleId, Node position)
        {
            InkUnit target = new() { Health = InkConsts.InkHealth };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            InkUnit target = new()
            {
                DashCooldown = DashCooldown,
                _dashUsedThisRound = _dashUsedThisRound,
            };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IInkAction;

        /// <summary>
        /// Returns visible ground and whirlpool targets within Chebyshev range, including the current grid.
        /// </summary>
        internal Node[] AttackNodes(Game game)
        {
            return game.Board
                .GetNodesWithinDistance(
                    Position,
                    AttackRange,
                    moveDirs: MapConsts.MoveChebyshev,
                    predicate: _ => true)
                .AsValueEnumerable()
                .Where(entry => game.Board.GetGrid(entry.node)!.Terrain is Terrain.Ground or Terrain.Whirlpool
                    && game.Board.CanLineShot(
                        Position,
                        entry.node,
                        MapConsts.DiagonalBlocked,
                        predicate: node => game.Board.GetGrid(node)!.CanShot(game)))
                .Select(entry => entry.node)
                .ToArray();
        }

        private bool CanAttackGeneral() => Alive && !End && AttackUsed < MaxAttackAction;

        internal bool CanAttack(InkAttack attack, Game game)
        {
            return CanAttackGeneral()
                && AttackNodes(game).AsValueEnumerable().Any(node => node == attack.Target);
        }

        internal void InkAttack(InkAttack attack, Game game)
        {
            ++AttackUsed;
            Grid grid = game.Board.GetGrid(attack.Target)!;
            int playerId = game.PlayerIdFromUnitId(Id);
            int? attackedBuildingId = null;
            Unit[] enemies = Array.Empty<Unit>();

            // A standing building shields units even when this pellet destroys it.
            if (grid.HasBlockingBuilding(game))
            {
                Building building = game.Buildings[grid.GridBuilding!.Value];
                attackedBuildingId = building.Id;
                building.ReceiveDirectDamage(BuildingAttackDamage, game, playerId);
            }
            else
            {
                enemies = grid.Units
                    .AsValueEnumerable()
                    .Select(unitId => game.GetUnit(unitId)!)
                    .Where(unit => unit.Alive
                        && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unit.Id)))
                    .ToArray();
                foreach (Unit enemy in enemies)
                {
                    int healthBefore = enemy.Health;
                    enemy.ReceiveDirectDamage(AttackDamage, game);
                    if (enemy.Health < healthBefore)
                    {
                        DashCooldown = 0;
                    }
                }
            }
            game.Events.Add(InkAttackEvent.New(
                attackerId: Id,
                unitIds: enemies.AsValueEnumerable().Select(unit => unit.Id).ToArray(),
                buildingId: attackedBuildingId,
                target: attack.Target));
        }

        /// <summary>
        /// Returns targets reached by two legal cardinal steps and their intermediate grids.
        /// </summary>
        private (Node node, int distance, Node? lastNode)[] DashNodes(Game game)
        {
            int playerId = game.PlayerIdFromUnitId(Id);
            return game.Board
                .GetNodesWithinDistance(
                    Position,
                    InkConsts.InkDashDistance,
                    moveDirs: MapConsts.MoveManhattan,
                    predicate: node => game.Board.GetGrid(node)!.CanStay(playerId, game))
                .AsValueEnumerable()
                .Where(entry => entry.distance == InkConsts.InkDashDistance)
                .ToArray();
        }

        private bool CanDashGeneral() => Alive && !End && DashCooldown == 0;

        internal bool CanDash(InkDash dash, Game game)
        {
            _lastDashPath = null;
            if (!CanDashGeneral())
            {
                return false;
            }
            foreach (var entry in DashNodes(game))
            {
                if (entry.node == dash.Target)
                {
                    _lastDashPath = new[] { Position, entry.lastNode!.Value, entry.node };
                    return true;
                }
            }
            return false;
        }

        internal void InkDash(Game game)
        {
            Node[] path = _lastDashPath!;
            DashCooldown = InkConsts.InkDashCooldownRounds;
            _dashUsedThisRound = true;
            for (int step = 1; step < path.Length; ++step)
            {
                ChangePos(path[step], game);
            }
            _lastDashPath = null;
            game.Events.Add(GeneralMoveEvent.New(Id, path));
        }

        public override void EndRound(Game game)
        {
            base.EndRound(game);
            if (!_dashUsedThisRound && DashCooldown > 0)
            {
                --DashCooldown;
            }
            _dashUsedThisRound = false;
            _lastDashPath = null;
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }
            InkUnitView view = (InkUnitView)GetView(game);
            return view.CanMoveGrids.Length > 0
                || view.CanAttackGrids.Length > 0
                || view.CanDashGrids.Length > 0
                || view.CanBuildFortGrids.Length > 0
                || view.CanBuildWallGrids.Length > 0
                || view.CanDemolishGrids.Length > 0;
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canBuild = active && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);
            return InkUnitView.New(
                canMoveGrids: Box.New(active && MoveUsed < MoveDistance
                    ? MoveEntries(game)
                    : Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canAttackGrids: Box.New(CanAttackGeneral() ? AttackNodes(game) : Array.Empty<Node>()),
                canDashGrids: Box.New(CanDashGeneral()
                    ? DashNodes(game).AsValueEnumerable().Select(entry => entry.node).ToArray()
                    : Array.Empty<Node>()),
                maxAttackAction: MaxAttackAction,
                attackUsed: AttackUsed,
                dashCooldown: DashCooldown,
                canBuildFortGrids: Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                canBuildWallGrids: Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>()),
                canDemolishGrids: Box.New(DemolishNodes(game))
            )!;
        }
    }

    /// <summary>
    /// Exposes Ink's available actions and a snapshot of its attack quota.
    /// </summary>
    public class InkUnitView : UnitView
    {
        public Node[] CanAttackGrids { get; private set; }
        public Node[] CanDashGrids { get; private set; }
        public int MaxAttackAction { get; private set; }
        public int AttackUsed { get; private set; }
        public int DashCooldown { get; private set; }

        private InkUnitView(Node[] canAttackGrids, Node[] canDashGrids) : base(null!, null!, null!, null!)
        {
            CanAttackGrids = canAttackGrids;
            CanDashGrids = canDashGrids;
        }

        public static InkUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canAttackGrids,
            Box<Node[]> canDashGrids,
            int maxAttackAction,
            int attackUsed,
            int dashCooldown,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids,
            Box<Node[]> canDemolishGrids)
        {
            if (canAttackGrids.TryTake() is not Node[] attackGrids)
            {
                return null;
            }
            if (canDashGrids.TryTake() is not Node[] dashGrids)
            {
                return null;
            }
            InkUnitView target = new(attackGrids, dashGrids)
            {
                MaxAttackAction = maxAttackAction,
                AttackUsed = attackUsed,
                DashCooldown = dashCooldown,
            };
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids, canDemolishGrids)
                ? target
                : null;
        }

        public override UnitView Clone()
        {
            InkUnitView target = new((Node[])CanAttackGrids.Clone(), (Node[])CanDashGrids.Clone())
            {
                MaxAttackAction = MaxAttackAction,
                AttackUsed = AttackUsed,
                DashCooldown = DashCooldown,
            };
            CloneBase(target);
            return target;
        }
    }

    /// <summary>
    /// Builds an Ink unit from its identifiers and starting position.
    /// </summary>
    public class InkUnitBuilder : UnitBuilder
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
            Built = InkUnit.New(id, roleId, position);
            return Built;
        }
    }

    /// <summary>
    /// Builds a role containing one Ink unit.
    /// </summary>
    public class InkBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private InkBuilder(UnitBuilder[] unitBuilder) : base(unitBuilder) { }

        public static InkBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not InkUnitBuilder)
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
            if (unitBuilder.Position != Position || unitBuilder.Build() is not InkUnit unit)
            {
                return null;
            }
            Built = Ink.New(id, playerId, unit);
            return Built;
        }
    }
}
