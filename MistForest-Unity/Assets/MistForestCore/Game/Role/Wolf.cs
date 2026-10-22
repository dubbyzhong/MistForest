using System;
using System.Diagnostics.Tracing;
using System.Dynamic;
using System.Xml;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using ZLinq;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IWolfAction { }

    public class WolfBite : Action, IWolfAction
    {
        public Node Target { get; private set; }

        public static WolfBite New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    public class Wolf : Role
    {
        private readonly Unit[] _units;
        public WolfUnit Unit => (WolfUnit)_units[0];

        private Wolf(WolfUnit unit) => _units = new Unit[] { unit };

        internal static Wolf New(int id, int playerId, WolfUnit unit)
        {
            Wolf target = new Wolf(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
        {
            return new Wolf(unit: (WolfUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };
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
                WolfBite bite => Unit.CanBite(bite, game),
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
                case WolfBite bite:
                    Unit.WolfBite(bite, game);
                    break;
            }
        }

        public override bool HasActionLeft(Game game) => !End(game);


        public override ReadOnlySpan<Unit> GetUnits() => _units;

    }

    public class WolfUnit : Unit
    {
        public override int Health { get; protected set; }
        protected override int MoveDistance => MaxAction - AttackUsed;
        public override int AttackRange => WolfConsts.WolfAttackRange;
        public int MaxAction => WolfConsts.WolfMaxAction;
        public int BiteDamage => WolfConsts.WolfBiteDamage;

        private WolfUnit()
            : base() { }

        private WolfUnit(Node position)
            : base(position) { }

        public static WolfUnit New(int id, int roleId, Node position)
        {
            var target = new WolfUnit(position) { Health = WolfConsts.WolfHealth };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            WolfUnit target = new() { Health = Health };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IWolfAction;


        internal override bool CanMove(GeneralMove move, Game game)
        {
            return base.CanMove(move, game) && (MoveUsed + AttackUsed < MaxAction);
        }

        internal Node[] BiteNodes(Game game)
        {
            return FarAttackNodes(game)
                .AsSpan()
                .AsValueEnumerable()
                .Where(node => game.Board.GetGrid(node)!.HasBlockingBuilding(game) is false)
                .ToArray();
        }

        internal bool CanBite(WolfBite bite, Game game)
        {
            if (AttackUsed + MoveUsed >= MaxAction)
            {
                return false;
            }

            return BiteNodes(game).AsValueEnumerable().Any(node => node == bite.Target);
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }

            return base.HasActionLeft(game)
                && AttackUsed + MoveUsed < MaxAction
                && GetView(game) is WolfUnitView view
                && (
                    view.CanBiteGrids.Length > 0
                    || view.CanMoveGrids.Length > 0
                    || view.CanBuildFortGrids.Length > 0
                    || view.CanBuildWallGrids.Length > 0
                );
        }

        public override UnitView GetView(Game game)
        {
            return WolfUnitView.New(
                canMoveGrids: Box.New(MoveEntries(game)),
                canBiteGrids: Box.New(BiteNodes(game)),
                canBuildFortGrids: Box.New(BuildFortNodes(game)),
                canBuildWallGrids: Box.New(BuildWallNodes(game))
            )!;
        }

        public void WolfBite(WolfBite bite, Game game)
        {
            AttackUsed++;
            Grid grid = game.Board.GetGrid(bite.Target)!;

            int attackerPlayerId = game.PlayerIdFromUnitId(Id);

            Unit[] enemies = grid
                .Units.AsValueEnumerable()
                .Where(unitId =>
                    !game.IsPlayerAllied(attackerPlayerId, game.PlayerIdFromUnitId(unitId))
                )
                .Select(unitId => game.GetUnit(unitId)!)
                .ToArray();

            foreach (Unit enemy in enemies)
            {
                enemy.ReceiveDirectDamage(BiteDamage, game);

                game.Events.Add(WolfBiteEvent.New(unitId: enemy.Id, biterId: Id));
            }
        }

        public override void EndRound(Game game)
        {
            base.EndRound(game);
        }
    }

    public class WolfUnitView : UnitView
    {
        public Node[] CanBiteGrids { get; private set; }

        private WolfUnitView(Node[] canBiteGrids)
            : base(null!, null!, null!)
        {
            CanBiteGrids = canBiteGrids;
        }

        public static WolfUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canBiteGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids
        )
        {
            if (canBiteGrids.TryTake() is not Node[] canBiteGridsArray)
            {
                return null;
            }
            WolfUnitView target = new(canBiteGridsArray);
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids) switch
            {
                false => null,
                true => target,
            };
        }

        public override UnitView Clone()
        {
            WolfUnitView target = new((Node[])CanBiteGrids.Clone());
            CloneBase(target);
            return target;
        }
    }

    public class WolfBiteEvent : Event
    {
        public int UnitId { get; private set; }
        public int BiterId { get; private set; }

        public static WolfBiteEvent New(int unitId, int biterId)
        {
            return new() { UnitId = unitId, BiterId = biterId };
        }
    }

    public class WolfUnitBuilder : UnitBuilder
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
            Built = WolfUnit.New(id, roleId, position);
            return Built;
        }
    }

    public class WolfBuilder : RoleBuilder
    {
        public Node Position { get; private set; }

        private WolfBuilder(UnitBuilder[] unitBuilder)
            : base(unitBuilder) { }

        public static WolfBuilder? New(UnitBuilder[] unitBuilder, Node position)
        {
            if (unitBuilder.Length != 1 || unitBuilder[0] is not WolfUnitBuilder)
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
            Built = Wolf.New(id, playerId, (WolfUnit)UnitBuilder[0].Build()!);
            return Built;
        }
    }
}
