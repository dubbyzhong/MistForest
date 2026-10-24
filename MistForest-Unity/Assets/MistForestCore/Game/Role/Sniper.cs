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
                GeneralDemolish demolish => Unit.CanDemolish(demolish, game),
                SniperExecute execute => Unit.CanExecute(execute, game),
                SniperScout scout => Unit.CanScout(scout, game),
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
                case SniperScout scout:
                    Unit.SniperScout(scout, game);
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
        public override int AttackRange { get; protected set; }
        private bool HasUnusedAction => MoveUsed == 0 && AttackUsed == 0;

        private SniperUnit()
            : base() { }

        private SniperUnit(Node position)
            : base(position) { }

        public static SniperUnit New(int id, int roleId, Node position, int attackRange)
        {
            var target = new SniperUnit(position)
            {
                Health = SniperConsts.SniperHealth,
                AttackRange = attackRange,
            };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            SniperUnit target = new() { Health = Health, AttackRange = AttackRange };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action)
        {
            return action is ISniperAction;
        }

        internal override bool CanMove(GeneralMove move, Game game)
        {
            return base.HasActionLeft(game) && HasUnusedAction && base.CanMove(move, game);
        }

        internal Node[] ExecuteNodes(Game game)
        {
            return FarAttackNodes(game);
        }

        internal bool CanExecute(SniperExecute execute, Game game)
        {
            if (!base.HasActionLeft(game) || !HasUnusedAction)
            {
                return false;
            }

            return ExecuteNodes(game).AsValueEnumerable().Any(node => node == execute.Target);
        }

        internal bool CanScout(SniperScout scout, Game game)
        {
            if (IsFirstAction is false)
            {
                return false;
            }
            return scout switch
            {
                SniperScoutRow scoutRow => scoutRow.Row >= 1 && scoutRow.Row <= game.Board.Height,
                SniperScoutCol scoutCol => scoutCol.Col >= 1 && scoutCol.Col <= game.Board.Width,
                _ => throw new NotImplementedException(),
            };
        }

        public override bool HasActionLeft(Game game)
        {
            return base.HasActionLeft(game)
                && HasUnusedAction
                && GetView(game) is SniperUnitView view
                && (
                    view.CanBuildFortGrids.Length > 0
                    || view.CanBuildWallGrids.Length > 0
                    || view.CanDemolishGrids.Length > 0
                    || view.CanMoveGrids.Length > 0
                    || view.CanExecuteGirds.Length > 0
                );
        }

        public override UnitView GetView(Game game)
        {
            return SniperUnitView.New(
                canMoveGrids: Box.New(MoveEntries(game)),
                canExecuteGrids: Box.New(ExecuteNodes(game)),
                canBuildFortGrids: Box.New(BuildFortNodes(game)),
                canBuildWallGrids: Box.New(BuildWallNodes(game)),
                canDemolishGrids: Box.New(DemolishNodes(game))
            )!;
        }

        public void SniperExecute(SniperExecute execute, Game game)
        {
            AttackUsed++;
            Grid grid = game.Board.GetGrid(execute.Target)!;
            Unit[] enemies = grid
                    .Units.AsValueEnumerable()
                    .Where(unitId =>
                        game.IsPlayerAllied(
                            game.PlayerIdFromUnitId(Id),
                            game.PlayerIdFromUnitId(unitId)
                        )
                            is false
                    )
                    .Select(unitId => game.GetUnit(unitId)!)
                    .ToArray();
            foreach (Unit unit in enemies)
            {
                unit.Killed(game);
            }
            int? attackedBuildingId = null;
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
                attackedBuildingId = buildingId;
                game.Buildings[buildingId].Destroyed(game, game.PlayerIdFromUnitId(Id));
            }
            game.Events.Add(ExecuteEvent.New(
                attackerId: Id,
                unitIds: enemies.AsValueEnumerable().Select(unit => unit.Id).ToArray(),
                buildingId: attackedBuildingId,
                target: execute.Target));
        }

        public void SniperScout(SniperScout scout, Game game)
        {
            End = true;
            if (scout is SniperScoutRow scoutRow)
            {
                game.Events.Add(
                    ScoutRowEvent.New(
                        Id,
                        success: (1..(game.Board.Width + 1))
                            .AsValueEnumerable()
                            .Any(y =>
                                game.Board.GetGrid(game.Board.At(scoutRow.Row, y)!.Value)!
                                    .Units.AsValueEnumerable()
                                    .Any(unit =>
                                        game.IsPlayerAllied(
                                            game.PlayerIdFromUnitId(Id),
                                            game.PlayerIdFromUnitId(unit)
                                        )
                                            is false
                                    )
                            ),
                        scoutRow.Row
                    )
                );
            }
            else if (scout is SniperScoutCol scoutCol)
            {
                game.Events.Add(
                    ScoutColEvent.New(
                        Id,
                        success: (1..(game.Board.Height + 1))
                            .AsValueEnumerable()
                            .Any(x =>
                                game.Board.GetGrid(game.Board.At(x, scoutCol.Col)!.Value)!
                                    .Units.AsValueEnumerable()
                                    .Any(unit =>
                                        game.IsPlayerAllied(
                                            game.PlayerIdFromUnitId(Id),
                                            game.PlayerIdFromUnitId(unit)
                                        )
                                            is false
                                    )
                            ),
                        scoutCol.Col
                    )
                );
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override void EndRound(Game game)
        {
            base.EndRound(game);
        }
    }

    public class SniperUnitView : UnitView
    {
        public Node[] CanExecuteGirds { get; private set; }

        private SniperUnitView(Node[] canExecuteGrids)
            : base(null!, null!, null!, null!)
        {
            CanExecuteGirds = canExecuteGrids;
        }

        public static SniperUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canExecuteGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids,
            Box<Node[]> canDemolishGrids
        )
        {
            if (canExecuteGrids.TryTake() is not Node[] canExecuteGridsArray)
            {
                return null;
            }

            SniperUnitView target = new(canExecuteGridsArray);
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids, canDemolishGrids) switch
            {
                false => null,
                true => target,
            };
        }

        public override UnitView Clone()
        {
            SniperUnitView target = new((Node[])CanExecuteGirds.Clone());
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

    public abstract class SniperScout : Action, ISniperAction, IScoutOnlyAction { }

    public class SniperScoutRow : SniperScout
    {
        public int Row { get; private set; }

        public static SniperScoutRow New(int callerUnitId, int row)
        {
            return new() { CallerUnitId = callerUnitId, Row = row };
        }
    }

    public class SniperScoutCol : SniperScout
    {
        public int Col { get; private set; }

        public static SniperScoutCol New(int callerUnitId, int column)
        {
            return new() { CallerUnitId = callerUnitId, Col = column };
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

    public class ExecuteEvent : AttackEvent
    {
        private ExecuteEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static ExecuteEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
        {
            return new(attackerId, unitIds, buildingId, target);
        }
    }

    public abstract class ScoutEvent : Event
    {
        public int UnitId { get; private set; }
        public bool Success { get; private set; }

        protected static void NewBase(ScoutEvent target, int unitId, bool success)
        {
            target.UnitId = unitId;
            target.Success = success;
        }
    }

    public class ScoutRowEvent : ScoutEvent
    {
        public int Row { get; private set; }

        public static ScoutRowEvent New(int unitId, bool success, int row)
        {
            ScoutRowEvent target = new() { Row = row };
            NewBase(target, unitId, success);
            return target;
        }
    }

    public class ScoutColEvent : ScoutEvent
    {
        public int Col { get; private set; }

        public static ScoutColEvent New(int unitId, bool success, int column)
        {
            ScoutColEvent target = new() { Col = column };
            NewBase(target, unitId, success);
            return target;
        }
    }
}
