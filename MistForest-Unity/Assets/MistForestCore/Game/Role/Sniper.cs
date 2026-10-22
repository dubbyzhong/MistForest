using System;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using ZLinq;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public class Sniper : Role
    {
        private readonly Unit[] _units;
        public SniperUnit Unit
        {
            get => (SniperUnit)_units[0];
        }

        private Sniper(SniperUnit unit) => _units = new Unit[] { unit };

        internal static Sniper New(int id, int playerId, SniperUnit unit)
        {
            Sniper target = new Sniper(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Sniper(unit: (SniperUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
        }

        public override bool JudgeAction(Action action, Game game)
        {
            return action switch
            {
                GeneralMove move => Unit.CanMove(move, game),
                GeneralEnd _ => Unit.CanEnd(),
                GeneralSurrender _ => Unit.CanSurrender(),
                GeneralBuildFort buildFort => Unit.CanBuildFort(buildFort, game),
                GeneralBuildWall buildWall => Unit.CanBuildWall(buildWall, game),
                SniperExecute execute => Unit.CanExecute(execute, game),
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
                case SniperExecute execute:
                    Unit.SniperExecute(execute, game);
                    break;
            }
        }

        public override bool HasActionLeft(Game game)
        {
            return !End(game);
        }

        public override ReadOnlySpan<Unit> GetUnits()
        {
            return _units;
        }
    }

    public class SniperUnit : Unit
    {
        public override int Health { get; protected set; }
        protected override int MoveDistance => SniperConsts.SniperMoveDistance;
        public bool ExecuteUsed { get; private set; }
        public int AttackRange { get; internal set; }

        private SniperUnit()
            : base() { }

        private SniperUnit(Node position)
            : base(position) { }

        public static SniperUnit New(int id, int roleId, Node position, int attackRange)
        {
            var target = new SniperUnit(position)
            {
                ExecuteUsed = false,
                Health = SniperConsts.SniperHealth,
                AttackRange = attackRange,
            };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            SniperUnit target = new()
            {
                Health = Health,
                AttackRange = AttackRange,
                ExecuteUsed = ExecuteUsed,
            };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action)
        {
            return action is ISniperAction;
        }

        internal override bool CanMove(GeneralMove move, Game game)
        {
            return base.CanMove(move, game) && !ExecuteUsed;
        }

        internal Node[] ExecuteNodes(Game game)
        {
            return game
                .Board.GetNodesWithinDistance(
                    Position,
                    AttackRange,
                    moveDirs: MapConsts.MoveManhattan,
                    predicate: _ => true
                )
                .AsValueEnumerable()
                .Where(entry =>
                    game.Board.CanLineShot(
                        Position,
                        entry.node,
                        MapConsts.DiagonalBlocked,
                        predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                    )
                    && game.Board.GetGrid(entry.node)!.Terrain
                        is Terrain.Ground
                            or Terrain.Whirlpool
                )
                .Select(entry => entry.node)
                .ToArray();
        }

        internal bool CanExecute(SniperExecute execute, Game game)
        {
            if (MoveUsed > 0 || ExecuteUsed)
            {
                return false;
            }
            if (game.Board.Distance(Position, execute.Target) > AttackRange)
            {
                return false;
            }
            return game.Board.CanLineShot(
                    Position,
                    execute.Target,
                    MapConsts.DiagonalBlocked,
                    predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                )
                && game.Board.GetGrid(execute.Target)!.Terrain
                    is Terrain.Ground
                        or Terrain.Whirlpool;
        }

        public override bool HasActionLeft(Game game)
        {
            return base.HasActionLeft(game)
                && MoveUsed < MoveDistance
                && !ExecuteUsed
                && GetView(game) is SniperUnitView view
                && view.CanExecuteGirds.Count() > 0
                && view.CanMoveGrids.Count() > 1;
        }

        public override UnitView GetView(Game game)
        {
            return SniperUnitView.New(
                canMoveGrids: Box.New(MoveEntries(game)),
                canExecuteGrids: Box.New(ExecuteNodes(game)),
                canBuildFortGrids: Box.New(BuildFortNodes(game)),
                canBuildWallGrids: Box.New(BuildWallNodes(game))
            )!;
        }

        public void SniperExecute(SniperExecute execute, Game game)
        {
            ExecuteUsed = true;
            Grid grid = game.Board.GetGrid(execute.Target)!;
            foreach (
                Unit unit in grid
                    .Units.AsValueEnumerable()
                    .Where(unitId =>
                        game.IsPlayerAllied(
                            game.PlayerIdFromUnitId(Id),
                            game.PlayerIdFromUnitId(unitId)
                        )
                            is false
                    )
                    .Select(unitId => game.GetUnit(unitId)!)
                    .ToArray()
            )
            {
                unit.Killed(game);
                game.Events.Add(ExecuteEvent.New(unitId: unit.Id, executorId: Id));
            }
            if (
                grid.GridBuilding is int buildingId
                && game.Buildings[buildingId] is not Building.Rubble
                && game.IsPlayerAllied(
                    game.Buildings[buildingId].Owner,
                    game.PlayerIdFromUnitId(Id)
                )
                    is false
            )
            {
                game.Buildings[buildingId].Destroyed(game, game.PlayerIdFromUnitId(Id));
            }
        }

        public override void EndRound(Game game)
        {
            ExecuteUsed = false;
            base.EndRound(game);
        }
    }

    public class SniperUnitView : UnitView
    {
        public Node[] CanExecuteGirds { get; private set; }

        private SniperUnitView(Node[] canExecuteGrids)
            : base(null!, null!, null!)
        {
            CanExecuteGirds = canExecuteGrids;
        }

        public static SniperUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canExecuteGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids
        )
        {
            if (canExecuteGrids.TryTake() is not Node[] canExecuteGridsArray)
            {
                return null;
            }

            SniperUnitView target = new(canExecuteGridsArray);
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids) switch
            {
                false => null,
                true => target,
            };
        }

        public override UnitView Clone()
        {
            SniperUnitView target = new(CanExecuteGirds);
            CloneBase(target);
            return target;
        }
    }

    public interface ISniperAction { }

    public class SniperExecute : Action, IScoutAction, ISniperAction
    {
        public Node Target { get; private set; }

        public static SniperExecute New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    public class SniperBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private SniperBuilder(UnitBuilder[] unitBuilder)
            : base(unitBuilder) { }

        public static SniperBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not SniperUnitBuilder)
            {
                return null;
            }
            return new(unitBuilder) { Position = position };
        }

        public override bool CanInitialStay(Board board)
        {
            if (board.GetGrid(Position) is not Grid grid)
            {
                return false;
            }
            return grid.CanInitialStay();
        }

        public override Role? Build()
        {
            if (Built is not null)
            {
                return Built;
            }
            if (Id is not int id)
            {
                return null;
            }
            if (PlayerId is not int playerId)
            {
                return null;
            }
            UnitBuilder[0].SetPosition(Position);
            Built = Sniper.New(id, playerId, (SniperUnit)UnitBuilder[0].Build()!);
            return Built;
        }
    }

    public class SniperUnitBuilder : UnitBuilder
    {
        private int _attackRange;

        public SniperUnitBuilder SetAttackRange(int attackRange)
        {
            _attackRange = attackRange;
            return this;
        }

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
            Built = SniperUnit.New(id, roleId, position, _attackRange);
            return Built;
        }
    }

    public class ExecuteEvent : Event
    {
        public int UnitId { get; private set; }
        public int ExecutorId { get; private set; }

        public static ExecuteEvent New(int unitId, int executorId)
        {
            return new() { UnitId = unitId, ExecutorId = executorId };
        }
    }
}
