using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IFireflyAction { }

    public class FireflySpray : Action, IFireflyAction
    {
        public Node First { get; private set; }
        public int AmmoCost { get; private set; }

        public static FireflySpray New(int callerUnitId, Node first, int ammoCost)
            => new() { CallerUnitId = callerUnitId, First = first, AmmoCost = ammoCost };
    }

    public class FireflyAdvance : Action, IFireflyAction
    {
        public Node Target { get; private set; }

        public static FireflyAdvance New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class FireflyStep : Action, IFireflyAction
    {
        public Node Target { get; private set; }
        public bool Ignite { get; private set; }

        public static FireflyStep New(int callerUnitId, Node target, bool ignite)
            => new() { CallerUnitId = callerUnitId, Target = target, Ignite = ignite };
    }

    public class Firefly : Role
    {
        private readonly Unit[] _units;
        public FireflyUnit Unit => (FireflyUnit)_units[0];

        private Firefly(FireflyUnit unit) => _units = new Unit[] { unit };

        internal static Firefly New(int id, int playerId, FireflyUnit unit)
        {
            Firefly role = new(unit);
            NewBase(role, id, playerId);
            return role;
        }

        public override Role Clone() => New(Id, PlayerId, (FireflyUnit)Unit.Clone());

        public override ReadOnlySpan<Unit> GetUnits() => _units;

        public override bool HasActionLeft(Game game) => !End(game);

        public override bool JudgeAction(Action action, Game game)
        {
            if (!Unit.Alive || Unit.End || action.CallerUnitId != Unit.Id)
            {
                return false;
            }
            return action switch
            {
                FireflyAdvance advance => Unit.CanAdvance(advance.Target, game),
                GeneralSurrender _ => JudgeGeneralAction(action, game),
                _ when Unit.IsAwaitingAdvance => false,
                FireflySpray spray => Unit.CanSpray(spray, game),
                FireflyStep step => Unit.CanStep(step.Target, game),
                _ => JudgeGeneralAction(action, game),
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            Unit.StartExecuteAction();
            switch (action)
            {
                case FireflySpray spray:
                    Unit.Spray(spray, game);
                    break;
                case FireflyAdvance advance:
                    Unit.Advance(advance.Target, game);
                    break;
                case FireflyStep step:
                    Unit.Step(step, game);
                    break;
                default:
                    ExecuteGeneralAction(action, game);
                    break;
            }
        }
    }

    public class FireflyUnit : Unit
    {
        private bool _usedFireStep;
        private bool _skipAmmoRecovery;
        public int Ammo { get; private set; } = FireflyConsts.FireflyMaxAmmo;
        internal bool IsAwaitingAdvance { get; private set; }
        protected override int MoveDistance => FireflyConsts.FireflyMoveDistance;

        private FireflyUnit() { }

        internal static FireflyUnit New(int id, int roleId, Node position)
        {
            FireflyUnit unit = new() { Health = FireflyConsts.FireflyHealth };
            NewBase(unit, id, roleId, position);
            return unit;
        }

        public override Unit Clone()
        {
            FireflyUnit clone = new()
            {
                Ammo = Ammo,
                _usedFireStep = _usedFireStep,
                _skipAmmoRecovery = _skipAmmoRecovery,
                IsAwaitingAdvance = IsAwaitingAdvance,
            };
            CloneBase(clone);
            return clone;
        }

        public override bool HasAction(Action action) => action is IFireflyAction;

        private bool HasMoveBudget => Alive && !End && !IsAwaitingAdvance && AttackUsed == 0 && MoveUsed < MoveDistance;
        private bool HasSprayBudget => HasMoveBudget && !IsSilenced && MoveUsed == 0 && Ammo > 0;
        private bool HasStepBudget => Alive && !End && !IsAwaitingAdvance && !IsSilenced
            && !_usedFireStep;

        internal override bool CanMove(GeneralMove move, Game game) => HasMoveBudget && base.CanMove(move, game);

        private Node[] SprayPath(Node first, int length, Game game)
        {
            Board board = game.Board;
            if (board.Distance(Position, first) != 1)
            {
                return Array.Empty<Node>();
            }

            int dx = board.RowOf(first)!.Value - board.RowOf(Position)!.Value;
            int dy = board.ColOf(first)!.Value - board.ColOf(Position)!.Value;
            List<Node> path = new(length);
            Node previous = Position;
            for (int distance = 0; distance < length; ++distance)
            {
                if (board.Move(previous, dx, dy) is not Node next
                    || board.GetGrid(next) is not Grid { Terrain: not Terrain.Wall } grid
                    || !board.CanLineShot(previous, next, MapConsts.DiagonalBlocked, _ => true))
                {
                    break;
                }

                path.Add(next);
                if (grid.HasBlockingBuilding(game))
                {
                    break;
                }
                previous = next;
            }
            return path.ToArray();
        }

        private (Node first, Node[] path)[] SprayTargets(Game game)
        {
            if (!HasSprayBudget)
            {
                return Array.Empty<(Node, Node[])>();
            }

            List<(Node first, Node[] path)> targets = new();
            int length = Math.Min(Ammo, FireflyConsts.FireflyMaxSprayRange);
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                if (game.Board.Move(Position, dx, dy) is Node first)
                {
                    Node[] path = SprayPath(first, length, game);
                    if (path.Length > 0)
                    {
                        targets.Add((first, path));
                    }
                }
            }
            return targets.ToArray();
        }

        internal bool CanSpray(FireflySpray spray, Game game)
            => HasSprayBudget && spray.AmmoCost > 0 && spray.AmmoCost <= Ammo
                && spray.AmmoCost <= FireflyConsts.FireflyMaxSprayRange
                && SprayPath(spray.First, spray.AmmoCost, game).Length > 0;

        internal void Spray(FireflySpray spray, Game game)
        {
            Node[] path = SprayPath(spray.First, spray.AmmoCost, game);
            Ammo -= spray.AmmoCost;
            ++AttackUsed;
            IsAwaitingAdvance = true;
            game.Events.Add(FireflySprayEvent.New(Id, spray.AmmoCost, path));
            foreach (Node target in path)
            {
                game.Ignite(Id, target);
            }
        }

        internal bool CanAdvance(Node target, Game game)
            => Alive && !End && IsAwaitingAdvance
                && (target == Position || !IsSilenced && game.IsBurning(target)
                    && game.Board.GetGrid(target)!.CanStay(game.PlayerIdFromUnitId(Id), game));

        private Node[] AdvanceTargets(Game game)
        {
            if (!Alive || End || !IsAwaitingAdvance)
            {
                return Array.Empty<Node>();
            }
            return game.GetBurningGrids()
                .Select(fire => fire.node)
                .Where(target => target != Position && CanAdvance(target, game))
                .Prepend(Position)
                .ToArray();
        }

        internal void Advance(Node target, Game game)
        {
            Node from = Position;
            IsAwaitingAdvance = false;
            if (target != from)
            {
                ChangePos(target, game, blink: true);
            }
            game.Events.Add(FireflyMoveEvent.New(game.PlayerIdFromUnitId(Id), Id, from, target));
        }

        private Node[] StepTargets(Game game)
        {
            if (!HasStepBudget)
            {
                return Array.Empty<Node>();
            }
            return game.Board.GetNodesWithinDistance(Position, FireflyConsts.FireflyMoveDistance,
                    MapConsts.MoveManhattan, MovePredicate(game))
                .Skip(1)
                .Select(entry => entry.node)
                .ToArray();
        }

        internal bool CanStep(Node target, Game game) => StepTargets(game).Contains(target);

        internal void Step(FireflyStep step, Game game)
        {
            Node from = Position;
            _usedFireStep = true;
            ChangePos(step.Target, game);
            game.Events.Add(FireflyStepEvent.New(Id));
            game.Events.Add(FireflyMoveEvent.New(game.PlayerIdFromUnitId(Id), Id, from, step.Target));
            if (step.Ignite)
            {
                _skipAmmoRecovery = true;
                game.Ignite(Id, step.Target);
            }
        }

        internal override DamageResult ReceiveBurningDamage(int damage, Game game) => default;

        public override void EndTurn(Game game)
        {
            base.EndTurn(game);
            if (!_skipAmmoRecovery)
            {
                int recovery = game.IsBurning(Position)
                    ? FireflyConsts.FireflyBurningTileRecovery
                    : FireflyConsts.FireflyAmmoRecovery;
                Ammo = Math.Min(FireflyConsts.FireflyMaxAmmo, Ammo + recovery);
            }
            _skipAmmoRecovery = false;
            _usedFireStep = false;
            IsAwaitingAdvance = false;
        }

        public override bool HasActionLeft(Game game)
            => Alive && !End && (IsAwaitingAdvance || HasMoveBudget && MoveEntries(game).Length > 0
                || SprayTargets(game).Length > 0 || StepTargets(game).Length > 0 || HasBuildOrDemolishTarget(game));

        public override UnitView GetView(Game game)
        {
            bool canBuild = Alive && !End && CanBuildCommon(game);
            return new FireflyUnitView(
                HasMoveBudget ? MoveEntries(game) : Array.Empty<(Node, int, Node?)>(),
                canBuild ? BuildFortNodes(game) : Array.Empty<Node>(),
                canBuild ? BuildWallNodes(game) : Array.Empty<Node>(),
                DemolishNodes(game), SprayTargets(game), AdvanceTargets(game), StepTargets(game), Ammo);
        }
    }

    public class FireflyUnitView : UnitView
    {
        public (Node first, Node[] path)[] CanSprayDirections { get; }
        public Node[] CanAdvanceGrids { get; }
        public Node[] CanStepGrids { get; }
        public int Ammo { get; }

        internal FireflyUnitView((Node node, int distance, Node? lastNode)[] moveGrids, Node[] buildFortGrids,
            Node[] buildWallGrids, Node[] demolishGrids, (Node first, Node[] path)[] sprayDirections,
            Node[] advanceGrids, Node[] stepGrids, int ammo)
            : base(moveGrids, buildFortGrids, buildWallGrids, demolishGrids)
        {
            CanSprayDirections = sprayDirections;
            CanAdvanceGrids = advanceGrids;
            CanStepGrids = stepGrids;
            Ammo = ammo;
        }

        public override UnitView Clone()
        {
            FireflyUnitView clone = new(CanMoveGrids, CanBuildFortGrids, CanBuildWallGrids, CanDemolishGrids,
                CanSprayDirections.Select(entry => (entry.first, (Node[])entry.path.Clone())).ToArray(),
                (Node[])CanAdvanceGrids.Clone(), (Node[])CanStepGrids.Clone(), Ammo);
            CloneBase(clone);
            return clone;
        }
    }

    public class FireflySprayEvent : Event
    {
        public int UnitId { get; }
        public int AmmoSpent { get; }
        public Node[] Path { get; }

        private FireflySprayEvent(int unitId, int ammoSpent, Node[] path)
        {
            UnitId = unitId;
            AmmoSpent = ammoSpent;
            Path = (Node[])path.Clone();
        }

        public static FireflySprayEvent New(int unitId, int ammoSpent, Node[] path)
            => new(unitId, ammoSpent, path);
    }

    public class FireflyStepEvent : Event
    {
        public int UnitId { get; }

        private FireflyStepEvent(int unitId) => UnitId = unitId;

        public static FireflyStepEvent New(int unitId) => new(unitId);
    }

    public class FireflyMoveEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public Node From { get; }
        public Node Target { get; }

        private FireflyMoveEvent(int playerId, int unitId, Node from, Node target)
        {
            PlayerId = playerId;
            UnitId = unitId;
            From = from;
            Target = target;
        }

        public static FireflyMoveEvent New(int playerId, int unitId, Node from, Node target)
            => new(playerId, unitId, from, target);

        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;
    }

    public class FireflyDamageEvent : AttackEvent
    {
        public bool IsBurningDamage { get; }

        private FireflyDamageEvent(int attackerId, int[] unitIds, int? buildingId, Node target, bool isBurningDamage)
            : base(attackerId, unitIds, buildingId, target) => IsBurningDamage = isBurningDamage;

        public static FireflyDamageEvent New(
            int attackerId, int[] unitIds, int? buildingId, Node target, bool isBurningDamage)
            => new(attackerId, unitIds, buildingId, target, isBurningDamage);
    }

    public class FireflyFireExpiredEvent : Event
    {
        public int PlayerId { get; }
        public Node[] Targets { get; }

        private FireflyFireExpiredEvent(int playerId, Node[] targets)
        {
            PlayerId = playerId;
            Targets = (Node[])targets.Clone();
        }

        public static FireflyFireExpiredEvent New(int playerId, Node[] targets) => new(playerId, targets);
    }

    public class FireflyUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is null && Id is int id && RoleId is int roleId && Position is Node position)
            {
                Built = FireflyUnit.New(id, roleId, position);
            }
            return Built;
        }
    }

    public class FireflyBuilder : RoleBuilder
    {
        public Node Position { get; }

        private FireflyBuilder(UnitBuilder[] unitBuilder, Node position) : base(unitBuilder)
            => Position = position;

        public static FireflyBuilder? New(UnitBuilder[] unitBuilder, Node position)
            => unitBuilder.Length == 1 && unitBuilder[0] is FireflyUnitBuilder
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
            if (builder.Position != Position || builder.Build() is not FireflyUnit unit)
            {
                return null;
            }
            Built = Firefly.New(id, playerId, unit);
            return Built;
        }
    }
}
