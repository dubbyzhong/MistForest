using System;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IAlphaAction { }

    public class Alpha : Role
    {
        private readonly Unit[] _units;
        public AlphaUnit Unit => (AlphaUnit)_units[0];

        private Alpha(AlphaUnit unit) => _units = new Unit[] { unit };

        internal static Alpha New(int id, int playerId, AlphaUnit unit)
        {
            Alpha target = new(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Alpha((AlphaUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
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

    public class AlphaUnit : Unit
    {
        protected override int MoveDistance => AlphaConsts.AlphaMoveDistance;
        public override int AttackRange => AlphaConsts.AlphaAttackRange;

        private AlphaUnit() { }

        public static AlphaUnit New(int id, int roleId, Node position)
        {
            AlphaUnit target = new() { Health = AlphaConsts.AlphaHealth };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            AlphaUnit target = new();
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IAlphaAction;

        internal override bool CanMove(GeneralMove move, Game game)
        {
            return AttackUsed == 0 && base.CanMove(move, game);
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }

            // TODO: Include attack and Blood availability when those mechanics are added.
            UnitView view = GetView(game);
            return view.CanMoveGrids.Length > 0
                || view.CanBuildFortGrids.Length > 0
                || view.CanBuildWallGrids.Length > 0;
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canMove = active && AttackUsed == 0 && MoveUsed < MoveDistance;
            bool canBuild = active && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);

            return AlphaUnitView.New(
                canMoveGrids: Box.New(canMove
                    ? MoveEntries(game)
                    : Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canBuildFortGrids: Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                canBuildWallGrids: Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>())
            )!;
        }
    }

    public class AlphaUnitView : UnitView
    {
        private AlphaUnitView()
            : base(null!, null!, null!) { }

        public static AlphaUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids
        )
        {
            AlphaUnitView target = new();
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids)
                ? target
                : null;
        }

        public override UnitView Clone()
        {
            AlphaUnitView target = new();
            CloneBase(target);
            return target;
        }
    }

    public class AlphaUnitBuilder : UnitBuilder
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
            Built = AlphaUnit.New(id, roleId, position);
            return Built;
        }
    }

    public class AlphaBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private AlphaBuilder(UnitBuilder[] unitBuilder)
            : base(unitBuilder) { }

        public static AlphaBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not AlphaUnitBuilder)
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
            UnitBuilder[0].SetPosition(Position);
            if (UnitBuilder[0].Build() is not AlphaUnit unit)
            {
                return null;
            }
            Built = Alpha.New(id, playerId, unit);
            return Built;
        }
    }
}
