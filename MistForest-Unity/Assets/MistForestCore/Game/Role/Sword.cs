using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface ISwordAction { }

    public class SwordStab : Action, ISwordAction
    {
        public Node Target { get; private set; }

        public static SwordStab New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class SwordAdvance : Action, ISwordAction
    {
        public Node Target { get; private set; }

        public static SwordAdvance New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class SwordThrow : Action, ISwordAction
    {
        public Node Target { get; private set; }

        public static SwordThrow New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class SwordRecall : Action, ISwordAction
    {
        public Node Target { get; private set; }

        public static SwordRecall New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class Sword : Role
    {
        private readonly Unit[] _units;
        public SwordUnit Unit => (SwordUnit)_units[0];

        private Sword(SwordUnit unit) => _units = new Unit[] { unit };

        internal static Sword New(int id, int playerId, SwordUnit unit)
        {
            Sword role = new(unit);
            NewBase(role, id, playerId);
            return role;
        }

        public override Role Clone() => New(Id, PlayerId, (SwordUnit)Unit.Clone());

        public override ReadOnlySpan<Unit> GetUnits() => _units;

        public override bool HasActionLeft(Game game) => !End(game);

        public override bool JudgeAction(Action action, Game game)
        {
            if (!Unit.Alive || Unit.End)
            {
                return false;
            }
            return action switch
            {
                SwordAdvance advance => advance.CallerUnitId == Unit.Id && Unit.CanAdvance(advance.Target, game),
                GeneralSurrender _ => JudgeGeneralAction(action, game),
                _ when Unit.IsAwaitingAdvance => false,
                SwordStab stab => stab.CallerUnitId == Unit.Id && Unit.CanStab(stab, game),
                SwordThrow swordThrow => swordThrow.CallerUnitId == Unit.Id && Unit.CanThrow(swordThrow.Target, game),
                SwordRecall recall => recall.CallerUnitId == Unit.Id && Unit.CanRecall(recall.Target, game),
                _ => JudgeGeneralAction(action, game),
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            Unit.StartExecuteAction();
            switch (action)
            {
                case SwordStab stab:
                    Unit.Stab(stab, game);
                    break;
                case SwordAdvance advance:
                    Unit.Advance(advance.Target, game);
                    break;
                case SwordThrow swordThrow:
                    Unit.Throw(swordThrow.Target, game);
                    break;
                case SwordRecall recall:
                    Unit.Recall(recall.Target, game);
                    break;
                default:
                    ExecuteGeneralAction(action, game);
                    break;
            }
        }
    }

    public class SwordUnit : Unit
    {
        private Node? _advanceTarget;
        private readonly List<Node> _placedSwords = new();
        public int SwordQi { get; private set; } = SwordConsts.SwordMaxQi;
        public int SwordQiCap => SwordConsts.SwordMaxQi - _placedSwords.Count;
        public int ExtraStabs { get; private set; }
        internal bool IsAwaitingAdvance => _advanceTarget is not null;
        protected override int MoveDistance => SwordConsts.SwordMoveDistance;
        public override int AttackRange => SwordConsts.SwordStabRange;

        private SwordUnit() { }

        internal static SwordUnit New(int id, int roleId, Node position)
        {
            SwordUnit unit = new() { Health = SwordConsts.SwordHealth };
            NewBase(unit, id, roleId, position);
            return unit;
        }

        public override Unit Clone()
        {
            SwordUnit clone = new() { SwordQi = SwordQi, ExtraStabs = ExtraStabs, _advanceTarget = _advanceTarget };
            clone._placedSwords.AddRange(_placedSwords);
            CloneBase(clone);
            return clone;
        }

        public override bool HasAction(Action action) => action is ISwordAction;

        private bool HasMoveBudget => !IsAwaitingAdvance && AttackUsed == 0 && MoveUsed < MoveDistance;

        private bool HasOrdinaryStab => AttackUsed == 0 && MoveUsed == 0;

        private bool HasStabBudget
            => Alive && !End && !IsAwaitingAdvance && (HasOrdinaryStab || ExtraStabs > 0)
                && SwordQi >= SwordConsts.SwordStabQiCost;

        internal override bool CanMove(GeneralMove move, Game game)
            => HasMoveBudget && base.CanMove(move, game);

        internal bool CanStab(SwordStab stab, Game game)
            => HasStabBudget && CanStabTarget(stab.Target, game);

        private bool CanStabTarget(Node target, Game game)
        {
            Board board = game.Board;
            return board.GetGrid(target) is Grid { Terrain: not Terrain.Wall }
                && board.Distance(Position, target) is int distance && distance <= AttackRange
                && board.CanLineShot(Position, target, MapConsts.DiagonalBlocked, _ => true);
        }

        internal void Stab(SwordStab stab, Game game)
        {
            if (!HasOrdinaryStab)
            {
                --ExtraStabs;
            }
            ++AttackUsed;
            SwordQi -= SwordConsts.SwordStabQiCost;
            Node target = stab.Target;
            var (unitIds, buildingId) = ResolveSingleGridAttack(target, SwordConsts.SwordStabDamage, DamageType.Melee, game);
            game.Events.Add(SwordStabEvent.New(Id, unitIds, buildingId, target));
            _advanceTarget = target;
        }

        internal bool CanAdvance(Node target, Game game)
        {
            return Alive && !End && _advanceTarget is Node attacked
                && (target == Position
                    || target == attacked && CanStabTarget(target, game)
                        && game.Board.GetGrid(target)!.CanStay(game.PlayerIdFromUnitId(Id), game));
        }

        private Node[] AdvanceTargets(Game game)
        {
            if (!Alive || End || _advanceTarget is not Node attacked)
            {
                return Array.Empty<Node>();
            }
            return attacked != Position && CanAdvance(attacked, game)
                ? new[] { Position, attacked }
                : new[] { Position };
        }

        internal void Advance(Node target, Game game)
        {
            Node from = Position;
            _advanceTarget = null;
            if (target != from)
            {
                ChangePos(target, game);
            }
            game.Events.Add(SwordAdvanceEvent.New(game.PlayerIdFromUnitId(Id), Id, from, target));
        }

        private (Node node, int qiCost)[] ThrowTargets(Game game)
        {
            if (!Alive || End || !IsFirstAction || IsSilenced || SwordQiCap <= 0
                || SwordQi < SwordConsts.SwordLeaveQiCost)
            {
                return Array.Empty<(Node, int)>();
            }

            Board board = game.Board;
            List<(Node node, int qiCost)> targets = new();
            if (board.GetGrid(Position) is Grid { Terrain: not Terrain.Wall })
            {
                targets.Add((Position, SwordConsts.SwordLeaveQiCost));
            }

            int range = Math.Min(SwordConsts.SwordThrowRange, SwordQi);
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                for (int distance = 1; distance <= range; ++distance)
                {
                    if (board.Move(Position, dx * distance, dy * distance) is not Node target
                        || board.GetGrid(target) is not Grid { Terrain: not Terrain.Wall }
                        || !board.CanLineShot(Position, target, MapConsts.DiagonalBlocked,
                            node => board.GetGrid(node)!.CanShot(game)))
                    {
                        break;
                    }
                    targets.Add((target, distance));
                }
            }
            return targets.ToArray();
        }

        internal bool CanThrow(Node target, Game game)
            => ThrowTargets(game).Any(entry => entry.node == target);

        internal void Throw(Node target, Game game)
        {
            int qiCost = target == Position ? SwordConsts.SwordLeaveQiCost : game.Board.Distance(Position, target)!.Value;
            _placedSwords.Add(target);
            SwordQi = Math.Min(SwordQiCap, SwordQi - qiCost);
            var (unitIds, buildingId) = ResolveSingleGridAttack(target, SwordConsts.SwordThrowDamage, DamageType.Melee, game);
            game.Events.Add(SwordThrowEvent.New(Id, unitIds, buildingId, target));
            End = true;
        }

        private bool HasRecallBudget => Alive && !End && !IsAwaitingAdvance && !IsSilenced;

        private int? RecallCost(Node target, Game game)
            => game.Board.Distance(Position, target) is int cost && cost <= SwordQi ? cost : null;

        private (Node node, int qiCost)[] RecallTargets(Game game)
        {
            if (!HasRecallBudget)
            {
                return Array.Empty<(Node, int)>();
            }

            List<(Node node, int qiCost)> targets = new();
            foreach (Node target in _placedSwords.Distinct())
            {
                if (RecallCost(target, game) is int cost)
                {
                    targets.Add((target, cost));
                }
            }
            return targets.ToArray();
        }

        internal bool CanRecall(Node target, Game game)
            => HasRecallBudget && _placedSwords.Contains(target) && RecallCost(target, game) is not null;

        internal void Recall(Node target, Game game)
        {
            SwordQi -= RecallCost(target, game)!.Value;
            _placedSwords.Remove(target);
            ++ExtraStabs;
            game.Events.Add(SwordRecallEvent.New(Id, target));
        }

        public override void EndTurn(Game game)
        {
            base.EndTurn(game);
            SwordQi = Math.Min(SwordQiCap, SwordQi + SwordConsts.SwordQiRecovery);
            ExtraStabs = 0;
            _advanceTarget = null;
        }

        public override bool HasActionLeft(Game game)
            => Alive && !End && (IsAwaitingAdvance
                || HasMoveBudget && MoveEntries(game).Length > 0
                || StabTargets(game).Length > 0
                || RecallTargets(game).Length > 0
                || HasBuildOrDemolishTarget(game));

        private Node[] StabTargets(Game game)
        {
            if (!HasStabBudget)
            {
                return Array.Empty<Node>();
            }

            List<Node> targets = new();
            if (CanStabTarget(Position, game))
            {
                targets.Add(Position);
            }
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                if (game.Board.Move(Position, dx, dy) is Node target && CanStabTarget(target, game))
                {
                    targets.Add(target);
                }
            }
            return targets.ToArray();
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canBuild = active && CanBuildCommon(game);
            return new SwordUnitView(
                active && HasMoveBudget ? MoveEntries(game) : Array.Empty<(Node, int, Node?)>(),
                canBuild ? BuildFortNodes(game) : Array.Empty<Node>(),
                canBuild ? BuildWallNodes(game) : Array.Empty<Node>(),
                DemolishNodes(game),
                StabTargets(game),
                AdvanceTargets(game),
                ThrowTargets(game),
                RecallTargets(game),
                _placedSwords.ToArray(),
                SwordQi,
                SwordQiCap,
                ExtraStabs);
        }
    }

    public class SwordUnitView : UnitView
    {
        public Node[] CanStabGrids { get; }
        public Node[] CanAdvanceGrids { get; }
        public (Node node, int qiCost)[] CanThrowGrids { get; }
        public (Node node, int qiCost)[] CanRecallGrids { get; }
        public Node[] PlacedSwordGrids { get; }
        public int SwordQi { get; }
        public int SwordQiCap { get; }
        public int ExtraStabs { get; }

        internal SwordUnitView(
            (Node node, int distance, Node? lastNode)[] moveGrids,
            Node[] buildFortGrids,
            Node[] buildWallGrids,
            Node[] demolishGrids,
            Node[] stabGrids,
            Node[] advanceGrids,
            (Node node, int qiCost)[] throwGrids,
            (Node node, int qiCost)[] recallGrids,
            Node[] placedSwordGrids,
            int swordQi,
            int swordQiCap,
            int extraStabs)
            : base(moveGrids, buildFortGrids, buildWallGrids, demolishGrids)
        {
            CanStabGrids = stabGrids;
            CanAdvanceGrids = advanceGrids;
            CanThrowGrids = throwGrids;
            CanRecallGrids = recallGrids;
            PlacedSwordGrids = placedSwordGrids;
            SwordQi = swordQi;
            SwordQiCap = swordQiCap;
            ExtraStabs = extraStabs;
        }

        public override UnitView Clone()
        {
            SwordUnitView clone = new(CanMoveGrids, CanBuildFortGrids, CanBuildWallGrids,
                CanDemolishGrids, (Node[])CanStabGrids.Clone(), (Node[])CanAdvanceGrids.Clone(),
                ((Node, int)[])CanThrowGrids.Clone(), ((Node, int)[])CanRecallGrids.Clone(),
                (Node[])PlacedSwordGrids.Clone(), SwordQi, SwordQiCap, ExtraStabs);
            CloneBase(clone);
            return clone;
        }
    }

    public class SwordStabEvent : AttackEvent
    {
        private SwordStabEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static SwordStabEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
            => new(attackerId, unitIds, buildingId, target);
    }

    public class SwordAdvanceEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public Node From { get; }
        public Node Target { get; }

        private SwordAdvanceEvent(int playerId, int unitId, Node from, Node target)
        {
            PlayerId = playerId;
            UnitId = unitId;
            From = from;
            Target = target;
        }

        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;

        public static SwordAdvanceEvent New(int playerId, int unitId, Node from, Node target)
            => new(playerId, unitId, from, target);
    }

    public class SwordThrowEvent : AttackEvent
    {
        private SwordThrowEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static SwordThrowEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
            => new(attackerId, unitIds, buildingId, target);
    }

    public class SwordRecallEvent : Event
    {
        public int UnitId { get; }
        public Node Target { get; }

        private SwordRecallEvent(int unitId, Node target)
        {
            UnitId = unitId;
            Target = target;
        }

        public static SwordRecallEvent New(int unitId, Node target) => new(unitId, target);
    }

    public class SwordUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is null && Id is int id && RoleId is int roleId && Position is Node position)
            {
                Built = SwordUnit.New(id, roleId, position);
            }
            return Built;
        }
    }

    public class SwordBuilder : RoleBuilder
    {
        public Node Position { get; }

        private SwordBuilder(UnitBuilder[] unitBuilder, Node position) : base(unitBuilder)
            => Position = position;

        public static SwordBuilder? New(UnitBuilder[] unitBuilder, Node position)
            => unitBuilder.Length == 1 && unitBuilder[0] is SwordUnitBuilder
                ? new(unitBuilder, position) : null;

        public override bool CanInitialStay(Board board)
            => (UnitBuilder[0].Position is not Node preset || preset == Position)
                && board.GetGrid(Position) is Grid grid && grid.CanInitialStay();

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
            UnitBuilder builder = UnitBuilder[0];
            if (builder.Position is null)
            {
                builder.SetPosition(Position);
            }
            if (builder.Position != Position || builder.Build() is not SwordUnit unit)
            {
                return null;
            }
            Built = Sword.New(id, playerId, unit);
            return Built;
        }
    }
}
