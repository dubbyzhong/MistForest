using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IWallGuardAction { }

    public class WallGuardAttack : Action, IWallGuardAction
    {
        public Node Target { get; private set; }

        public static WallGuardAttack New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class WallGuardBuildGreatWall : Action, IWallGuardAction
    {
        public Node Target { get; private set; }

        public static WallGuardBuildGreatWall New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class WallGuard : Role
    {
        private readonly Unit[] _units;

        private WallGuard(Unit[] units) => _units = units;

        internal static WallGuard New(int id, int playerId, Unit[] units)
        {
            WallGuard role = new(units);
            NewBase(role, id, playerId);
            return role;
        }

        public override Role Clone()
            => New(Id, PlayerId, _units.Select(unit => unit.Clone()).ToArray());

        public override ReadOnlySpan<Unit> GetUnits() => _units;

        public override bool HasActionLeft(Game game) => !End(game);

        public override bool JudgeAction(Action action, Game game)
        {
            if (GetCallerUnit(action) is not WallGuardUnit soldier || !soldier.Alive)
            {
                return false;
            }
            if (action is GeneralEnd || action is GeneralSurrender _)
            {
                return JudgeGeneralAction(action, game);
            }
            if (soldier.End)
            {
                return false;
            }
            return action switch
            {
                WallGuardAttack attack => soldier.CanAttack(attack.Target, game),
                WallGuardBuildGreatWall build => soldier.CanBuildGreatWall(build.Target, game),
                _ => JudgeGeneralAction(action, game),
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            if (GetCallerUnit(action) is not WallGuardUnit soldier || !soldier.HasAction(action))
            {
                return;
            }
            soldier.StartExecuteAction();
            switch (action)
            {
                case WallGuardAttack attack:
                    soldier.Attack(attack.Target, game);
                    break;
                case WallGuardBuildGreatWall build:
                    soldier.BuildGreatWall(build.Target, game);
                    break;
                case GeneralEnd _:
                    foreach (Unit unit in _units)
                    {
                        ((WallGuardUnit)unit).FinishAction();
                    }
                    ExecuteGeneralAction(action, game);
                    break;
                default:
                    ExecuteGeneralAction(action, game);
                    break;
            }
            soldier.FinishAction();
        }
    }

    public class WallGuardUnit : Unit
    {
        protected override int MoveDistance => WallGuardConsts.MoveDistance;
        public override int AttackRange => WallGuardConsts.GroundAttackRange;

        private WallGuardUnit() { }

        internal static WallGuardUnit New(int id, int roleId, Node position)
        {
            WallGuardUnit unit = new() { Health = WallGuardConsts.SoldierHealth };
            NewBase(unit, id, roleId, position);
            return unit;
        }

        public override Unit Clone()
        {
            WallGuardUnit clone = new();
            CloneBase(clone);
            return clone;
        }

        public override bool HasAction(Action action) => action is IWallGuardAction;

        internal void FinishAction() => End = true;

        private bool IsOnAlliedGreatWall(Game game)
        {
            return game.Board.GetGrid(Position)!.GridBuilding is int id
                && game.Buildings[id] is Building.GreatWall wall
                && game.IsPlayerAllied(game.PlayerIdFromUnitId(Id), wall.Owner);
        }

        private int GetAttackRange(Game game)
            => IsOnAlliedGreatWall(game) ? WallGuardConsts.WallAttackRange : AttackRange;

        internal bool CanAttack(Node target, Game game)
            => Array.IndexOf(FarAttackNodes(game, GetAttackRange(game)), target) >= 0;

        internal bool CanBuildGreatWall(Node target, Game game)
            => CanBuildCommon(game) && Array.IndexOf(BuildWallNodes(game), target) >= 0;

        internal void Attack(Node target, Game game)
        {
            game.DamageRoseAt(Id, target, WallGuardConsts.AttackDamage);
            Grid grid = game.Board.GetGrid(target)!;
            int playerId = game.PlayerIdFromUnitId(Id);
            if (grid.HasBlockingBuilding(game))
            {
                int buildingId = grid.GridBuilding!.Value;
                game.Buildings[buildingId].ReceiveDirectDamage(WallGuardConsts.AttackDamage, game, playerId);
                game.Events.Add(WallGuardAttackEvent.New(Id, Array.Empty<int>(), buildingId, target));
                return;
            }

            DamageType type = IsOnAlliedGreatWall(game) ? DamageType.Ranged : DamageType.Melee;
            List<int> attacked = new(grid.Units.Count);
            foreach (int unitId in grid.Units.ToArray())
            {
                Unit enemy = game.GetUnit(unitId)!;
                if (enemy.Alive && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unitId)))
                {
                    attacked.Add(unitId);
                    enemy.ReceiveDirectDamage(WallGuardConsts.AttackDamage, game, type);
                }
            }
            game.Events.Add(WallGuardAttackEvent.New(Id, attacked.ToArray(), null, target));
        }

        internal void BuildGreatWall(Node target, Game game)
        {
            bool success = game.Board.GetGrid(target)!.Units.Count == 0;
            if (success)
            {
                game.AddBuilding(GreatWallBuilder.New()
                    .SetOwner(game.PlayerIdFromUnitId(Id))!
                    .SetPos(target)!
                    .SetHealth(WallGuardConsts.GreatWallHealth)!);
            }
            game.Events.Add(WallGuardBuildGreatWallEvent.New(Id, target, success));
        }

        protected override int ApplyDirectHealthDamage(int damage, Game game)
        {
            ReadOnlySpan<Unit> soldiers = game.Roles[RoleId].GetUnits();
            List<WallGuardUnit> connected = new(soldiers.Length) { this };
            for (int i = 0; i < connected.Count; i++)
            {
                foreach (Unit unit in soldiers)
                {
                    WallGuardUnit soldier = (WallGuardUnit)unit;
                    if (soldier.Alive && !connected.Contains(soldier)
                        && AreConnected(connected[i].Position, soldier.Position, game.Board))
                    {
                        connected.Add(soldier);
                    }
                }
            }
            if (connected.Count == 1)
            {
                return base.ApplyDirectHealthDamage(damage, game);
            }

            int share = damage / connected.Count;
            // Current strengths divide evenly. Assign any remainder starting with the struck soldier.
            int remainder = damage % connected.Count;
            int deaths = 0;
            for (int i = 0; i < connected.Count; i++)
            {
                if (connected[i].Health <= share + (i < remainder ? 1 : 0))
                {
                    deaths++;
                }
            }
            if (deaths > (Health <= damage ? 1 : 0))
            {
                return base.ApplyDirectHealthDamage(damage, game);
            }

            int healthDamage = 0;
            for (int i = 0; i < connected.Count; i++)
            {
                healthDamage += connected[i].ApplyHealthDamage(share + (i < remainder ? 1 : 0), game);
            }
            return healthDamage;
        }

        private static bool AreConnected(Node from, Node to, Board board)
            => Math.Abs(board.RowOf(from)!.Value - board.RowOf(to)!.Value) <= 1
                && Math.Abs(board.ColOf(from)!.Value - board.ColOf(to)!.Value) <= 1;

        public override UnitView GetView(Game game)
        {
            bool canAct = Alive && !End;
            int range = GetAttackRange(game);
            return new WallGuardUnitView(
                canAct ? MoveEntries(game) : Array.Empty<(Node, int, Node?)>(),
                canAct ? FarAttackNodes(game, range) : Array.Empty<Node>(),
                canAct && CanBuildCommon(game) ? BuildWallNodes(game) : Array.Empty<Node>(),
                DemolishNodes(game),
                range);
        }
    }

    public class WallGuardUnitView : UnitView
    {
        public Node[] CanAttackGrids { get; }
        public Node[] CanBuildGreatWallGrids { get; }
        public int AttackRange { get; }

        internal WallGuardUnitView(
            (Node node, int distance, Node? lastNode)[] moveGrids,
            Node[] attackGrids,
            Node[] greatWallGrids,
            Node[] demolishGrids,
            int attackRange)
            : base(moveGrids, Array.Empty<Node>(), Array.Empty<Node>(), demolishGrids)
        {
            CanAttackGrids = attackGrids;
            CanBuildGreatWallGrids = greatWallGrids;
            AttackRange = attackRange;
        }

        public override UnitView Clone()
        {
            WallGuardUnitView view = new(CanMoveGrids, (Node[])CanAttackGrids.Clone(),
                (Node[])CanBuildGreatWallGrids.Clone(), CanDemolishGrids, AttackRange);
            CloneBase(view);
            return view;
        }
    }

    public class WallGuardAttackEvent : AttackEvent
    {
        private WallGuardAttackEvent(int attackerId, int[] units, int? buildingId, Node target)
            : base(attackerId, units, buildingId, target) { }

        public static WallGuardAttackEvent New(int attackerId, int[] units, int? buildingId, Node target)
            => new(attackerId, units, buildingId, target);
    }

    public class WallGuardBuildGreatWallEvent : Event
    {
        public int UnitId { get; private set; }
        public Node Target { get; private set; }
        public bool Success { get; private set; }

        public static WallGuardBuildGreatWallEvent New(int unitId, Node target, bool success)
            => new() { UnitId = unitId, Target = target, Success = success };
    }

    public class WallGuardUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is null && Id is int id && RoleId is int roleId && Position is Node position)
            {
                Built = WallGuardUnit.New(id, roleId, position);
            }
            return Built;
        }
    }

    public class WallGuardBuilder : RoleBuilder
    {
        private readonly Node[] _positions;

        private WallGuardBuilder(UnitBuilder[] units, Node[] positions) : base(units)
            => _positions = positions;

        public static WallGuardBuilder? New(UnitBuilder[] units, Node[] positions)
        {
            if (units.Length != WallGuardConsts.SoldierCount || positions.Length != units.Length
                || units.Any(unit => unit is not WallGuardUnitBuilder)
                || units.Distinct().Count() != units.Length)
            {
                return null;
            }
            return new((UnitBuilder[])units.Clone(), (Node[])positions.Clone());
        }

        public override bool CanInitialStay(Board board)
        {
            for (int i = 0; i < _positions.Length; i++)
            {
                Node position = _positions[i];
                if ((UnitBuilder[i].Position is Node preset && preset != position)
                    || board.GetGrid(position) is not Grid grid || !grid.CanInitialStay())
                {
                    return false;
                }
            }
            return true;
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
            Unit[] soldiers = new Unit[_positions.Length];
            for (int i = 0; i < soldiers.Length; i++)
            {
                UnitBuilder builder = UnitBuilder[i];
                if (builder.Position is null)
                {
                    builder.SetPosition(_positions[i]);
                }
                if (builder.Position != _positions[i] || builder.Build() is not WallGuardUnit soldier)
                {
                    return null;
                }
                soldiers[i] = soldier;
            }
            Built = WallGuard.New(id, playerId, soldiers);
            return Built;
        }
    }
}
