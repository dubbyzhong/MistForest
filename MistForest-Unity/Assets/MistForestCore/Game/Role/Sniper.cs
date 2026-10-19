using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public class Sniper : Role
    {
        public SniperUnit Unit { get; private set; }

        private Sniper(SniperUnit unit) => Unit = unit;

        internal static Sniper New(int id, int playerId, SniperUnit unit)
        {
            return new Sniper(unit) { Id = id, PlayerId = playerId };
        }

        public override Role Clone()
        {
            return new Sniper(unit: (SniperUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
        }

        public bool JudgeAction(Action action, Game game)
        {
            return action switch
            {
                GeneralMove move => Unit.CanMove(move, game),
                GeneralEnd _ => Unit.CanEnd(),
                GeneralSurrender _ => Unit.CanSurrender(),
                SniperExecute execute => Unit.CanExecute(execute, game),
                _ => false,
            };
        }

        public void ExecuteAction<T>(T action, Game game)
            where T : Action
        {
            switch (action)
            {
                case GeneralMove move:
                    Unit.GeneralMove(move, game);
                    break;
                case GeneralEnd _:
                    GeneralEnd(game);
                    break;
                case GeneralSurrender _:
                    GeneralSurrender(game);
                    break;
                case SniperExecute execute:
                    Unit.SniperExecute(execute, game);
                    break;
            }
        }

        public override bool HasActionLeft()
        {
            if (End)
            {
                return false;
            }
            End = !Unit.HasActionLeft();
            return !End;
        }

        public override Unit[] GetUnits()
        {
            return new[] { Unit };
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

        public static SniperUnit New(int Id, int roleId, Node position, int attackRange)
        {
            var target = new SniperUnit(position)
            {
                ExecuteUsed = false,
                Health = SniperConsts.SniperHealth,
                AttackRange = attackRange,
            };
            return (SniperUnit)NewBase(target, Id, roleId, position);
        }

        public override Unit Clone()
        {
            return CloneBase(new SniperUnit());
        }

        internal override bool CanMove(GeneralMove move, Game game)
        {
            return base.CanMove(move, game) && !ExecuteUsed;
        }

        internal bool CanExecute(SniperExecute execute, Game game)
        {
            if (MoveUsed > 0)
            {
                return false;
            }
            if (game.Board.Distance(Position, execute.Target) > AttackRange)
            {
                return false;
            }
            return game.Board.CanLineShot(Position, execute.Target, MapConsts.DiagonalBlocked);
        }

        public override bool HasActionLeft()
        {
            return MoveUsed < MoveDistance && !ExecuteUsed;
        }

        public override void EndRound(Game game)
        {
            EndRoundBase(game);
        }

        public void SniperExecute(SniperExecute execute, Game game)
        {
            ExecuteUsed = true;
            Grid grid = game.Board.GetGrid(execute.Target)!;
            foreach (Unit unit in grid.Units.Select(unitId => game.GetUnit(unitId)!).ToArray())
            {
                unit.Killed(game);
                game.Events.Add(ExecuteEvent.New(unitId: unit.Id, executorId: Id));
            }
        }
    }

    public interface ISniperAction { }

    public class SniperExecute : Action, ISniperAction
    {
        public Node Target { get; private set; }

        public static SniperExecute New(Node target)
        {
            return new() { Target = target };
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
