using System.Diagnostics.Tracing;
using System.Xml;
using MistForest.Core.Consts;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;
using MistForest.Core.GameMap;
using ZLinq;
using System.Dynamic;
using System;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IWolfAction { }
    /// <summary>
    /// wolf's attack class
    /// </summary>
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
        public WolfUnit Unit
        {
            get => (WolfUnit)_units[0];
        }
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
        public override bool HasActionLeft(Game game)
        {
            return !End(game);
        }
        public override ReadOnlySpan<Unit> GetUnits()
        {
            return _units;
        }
    }
    public class WolfUnit : Unit
    {
        public override int Health { get; protected set; }
        protected override int MoveDistance => WolfConsts.WolfMoveDistance;
        public int AttackRange => WolfConsts.WolfAttackRange;
        public int MaxAction => WolfConsts.WolfMaxAction;
        public int BiteDamage => WolfConsts.WolfBiteDamage;
        private WolfUnit() : base() { }
        private WolfUnit(Node position) : base(position) { }
        public static WolfUnit New(int id, int roleId, Node position)
        {
            var target = new WolfUnit(position)
            {
                Health = WolfConsts.WolfHealth,
            };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            WolfUnit target = new()
            {
                Health = Health,
            };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action)
        {
            return action is IWolfAction;
        }

        internal override bool CanMove(GeneralMove move, Game game)
        {
            return base.CanMove(move, game) && (MoveUsed + AttackUsed < MaxAction);
        }
        internal Node[] BiteNodes(Game game)
        {

            return game.Board
                .GetNodesWithinDistance(
                    Position,
                    AttackRange,
                    moveDirs: MapConsts.MoveManhattan,
                    predicate: _ => true
                )
                .AsValueEnumerable()
                .Where(entry =>
                {
                    Grid grid = game.Board.GetGrid(entry.node)!;

                    return (grid.Terrain is Terrain.Ground or Terrain.Whirlpool)
                        && grid.CanShot(game)
                        && game.Board.CanLineShot(
                            Position,
                            entry.node,
                            MapConsts.DiagonalBlocked,
                            predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                        );
                })
                .Select(entry => entry.node)
                .ToArray();
        }
        internal bool CanBite(WolfBite bite, Game game)
        {
            if (AttackUsed + MoveUsed >= MaxAction)
            {
                return false;
            }

            return BiteNodes(game)
                .AsValueEnumerable()
                .Any(node => node == bite.Target);
        }
        public override bool HasActionLeft(Game game)
        {
            return base.HasActionLeft(game)
            &&
            AttackUsed + MoveUsed < MaxAction
            && GetView(game) is WolfUnitView view
            && view.CanBiteGrids.Length > 0
                && view.CanMoveGrids.Length > 1; ;
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
            game.Events.Add(new WolfBiteEvent
            {
                UnitId = Id,
                Target = bite.Target
            });
            if (grid.GridBuilding is int buildingId
        && game.Buildings[buildingId] is not Building.Rubble)
            {
                return;
            }
            int attackerPlayerId = game.PlayerIdFromUnitId(Id);

            Unit[] enemies = grid.Units
                .AsValueEnumerable()
                .Where(unitId => !game.IsPlayerAllied(
                    attackerPlayerId,
                    game.PlayerIdFromUnitId(unitId)
                ))
                .Select(unitId => game.GetUnit(unitId)!)
                .ToArray();

            foreach (Unit enemy in enemies)
            {
                enemy.ReceiveDirectDamage(BiteDamage, game);
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
        private WolfUnitView(Node[] canBiteGrids) : base(null!, null!, null!)
        {
            CanBiteGrids = canBiteGrids;
        }
        public static WolfUnitView? New(Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
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
            WolfUnitView target = new(CanBiteGrids);
            CloneBase(target);
            return target;
        }
    }
    public class WolfBiteEvent : Event
    {
        public int UnitId;
        public Node Target;
    }
}
