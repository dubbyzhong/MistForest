using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IKnightAction { }

    /// <summary>
    /// Follow-up options available while hunting. The numeric order is meaningful:
    /// each choice must be greater than the previous one.
    /// </summary>
    public enum KnightOption
    {
        None = 0,
        Silence = 1,
        Cleave = 2,
        SecretLeap = 3,
    }

    public class KnightLeap : Action, IKnightAction
    {
        public Node Target { get; private set; }

        public static KnightLeap New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class KnightHunt : Action, IKnightAction
    {
        public Node Target { get; private set; }
        public static KnightHunt New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class KnightSilence : Action, IKnightAction
    {
        public static KnightSilence New(int callerUnitId) => new() { CallerUnitId = callerUnitId };
    }

    public class KnightCleave : Action, IKnightAction
    {
        public Node Target { get; private set; }
        public static KnightCleave New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class KnightCleaveRush : Action, IKnightAction
    {
        public Node Target { get; private set; }
        public static KnightCleaveRush New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class KnightSecretLeap : Action, IKnightAction
    {
        public Node Target { get; private set; }
        public static KnightSecretLeap New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class Knight : Role
    {
        private readonly Unit[] _units;
        public KnightUnit Unit => (KnightUnit)_units[0];

        private Knight(KnightUnit unit) => _units = new Unit[] { unit };

        internal static Knight New(int id, int playerId, KnightUnit unit)
        {
            Knight target = new(unit);
            NewBase(target, id, playerId);
            return target;
        }

        public override Role Clone()
            => new Knight((KnightUnit)Unit.Clone()) { Id = Id, PlayerId = PlayerId };

        public override bool JudgeAction(Action action, Game game)
        {
            if (!Unit.Alive || Unit.End)
            {
                return false;
            }
            return action switch
            {
                KnightLeap leap => Unit.CanLeap(leap, game),
                KnightHunt hunt => Array.IndexOf(Unit.HuntNodes(game), hunt.Target) >= 0,
                KnightSilence _ => Unit.CanSilence,
                KnightCleave cleave => Array.IndexOf(Unit.CleaveNodes(game), cleave.Target) >= 0,
                KnightCleaveRush rush => Array.IndexOf(Unit.CleaveRushNodes(game), rush.Target) >= 0,
                KnightSecretLeap leap => Array.IndexOf(Unit.SecretLeapNodes(game), leap.Target) >= 0,
                GeneralEnd _ => Unit.CanEnd(),
                GeneralSurrender _ => Unit.CanSurrender(),
                GeneralBuildFort fort => Unit.CanBuildFort(fort, game),
                GeneralBuildWall wall => Unit.CanBuildWall(wall, game),
                GeneralDemolish demolish => Unit.CanDemolish(demolish, game),
                _ => false,
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            Unit.StartExecuteAction();
            switch (action)
            {
                case KnightLeap leap: Unit.Leap(leap, game); break;
                case KnightHunt hunt: Unit.Hunt(hunt, game); break;
                case KnightSilence _: Unit.Silence(game); break;
                case KnightCleave cleave: Unit.Cleave(cleave.Target, false, game); break;
                case KnightCleaveRush rush: Unit.Cleave(rush.Target, true, game); break;
                case KnightSecretLeap leap: Unit.SecretLeap(leap, game); break;
                case GeneralEnd end: Unit.GeneralEnd(end, game); break;
                case GeneralSurrender _: GeneralSurrender(game); break;
                case GeneralBuildFort fort: Unit.GeneralBuildFort(fort, game); break;
                case GeneralBuildWall wall: Unit.GeneralBuildWall(wall, game); break;
                case GeneralDemolish demolish: Unit.GeneralDemolish(demolish, game); break;
            }
        }

        public override bool HasActionLeft(Game game) => !End(game);
        public override ReadOnlySpan<Unit> GetUnits() => _units;
    }

    public class KnightUnit : Unit
    {
        protected override int MoveDistance => 0;
        public override int AttackRange => 0;
        public bool IsHunting { get; private set; }
        public Node HuntOrigin { get; private set; }
        public int OptionsUsed { get; private set; }
        public KnightOption LastOption { get; private set; }
        public int SilenceCooldown => _silenceCooldown.Remaining;
        public int CleaveCooldown => _cleaveCooldown.Remaining;
        public int SecretLeapCooldown => _secretLeapCooldown.Remaining;
        private Cooldown _silenceCooldown;
        private Cooldown _cleaveCooldown;
        private Cooldown _secretLeapCooldown;

        private KnightUnit() { }

        public static KnightUnit New(int id, int roleId, Node position)
        {
            KnightUnit target = new() { Health = KnightConsts.KnightHealth };
            NewBase(target, id, roleId, position);
            return target;
        }

        public override Unit Clone()
        {
            KnightUnit target = new()
            {
                IsHunting = IsHunting, HuntOrigin = HuntOrigin, OptionsUsed = OptionsUsed,
                LastOption = LastOption, _silenceCooldown = _silenceCooldown,
                _cleaveCooldown = _cleaveCooldown, _secretLeapCooldown = _secretLeapCooldown,
            };
            CloneBase(target);
            return target;
        }

        public override bool HasAction(Action action) => action is IKnightAction;

        internal Node[] LeapNodes(Game game)
        {
            if (!Alive || End || !IsFirstAction)
            {
                return Array.Empty<Node>();
            }
            return JumpNodes(game);
        }

        private Node[] JumpNodes(Game game)
        {
            int playerId = game.PlayerIdFromUnitId(Id);
            List<Node> targets = new();
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int rowOffset = dx * KnightConsts.KnightLeapLongOffset
                        + dy * side * KnightConsts.KnightLeapShortOffset;
                    int colOffset = dy * KnightConsts.KnightLeapLongOffset
                        + dx * side * KnightConsts.KnightLeapShortOffset;
                    if (JumpTarget(Position, rowOffset, colOffset, playerId, game) is Node target)
                    {
                        targets.Add(target);
                    }
                }
            }
            return targets.ToArray();
        }

        /// <summary>Shared jump rules; attachment copies may wrap the board boundary.</summary>
        internal static Node? JumpTarget(Node from, int dx, int dy, int playerId, Game game, bool wrap = false)
        {
            if (Math.Abs(dx) * Math.Abs(dy)
                != KnightConsts.KnightLeapLongOffset * KnightConsts.KnightLeapShortOffset) return null;
            Board board = game.Board;
            int legDx = Math.Abs(dx) == KnightConsts.KnightLeapLongOffset ? Math.Sign(dx) : 0;
            int legDy = Math.Abs(dy) == KnightConsts.KnightLeapLongOffset ? Math.Sign(dy) : 0;
            Node? leg = wrap ? ShadowUnit.Step(from, legDx, legDy, game) : board.Move(from, legDx, legDy);
            if (leg is not Node legNode || board.GetGrid(legNode)!.Terrain == Terrain.Wall
                || (!wrap && !board.CanLineShot(from, legNode, MapConsts.DiagonalBlocked, _ => true)))
                return null;
            Node? target = wrap
                ? board.At(((board.RowOf(from)!.Value - 1 + dx) % board.Height + board.Height) % board.Height + 1,
                    ((board.ColOf(from)!.Value - 1 + dy) % board.Width + board.Width) % board.Width + 1)
                : board.Move(from, dx, dy);
            return target is Node node && board.GetGrid(node)!.CanStay(playerId, game) ? node : null;
        }

        internal bool CanLeap(KnightLeap leap, Game game)
            => Array.IndexOf(LeapNodes(game), leap.Target) >= 0;

        internal void Leap(KnightLeap leap, Game game)
        {
            Node from = Position;
            ChangePos(leap.Target, game);
            End = true;
            game.Events.Add(KnightLeapEvent.New(Id));
            game.Events.Add(KnightLeapResultEvent.New(game.PlayerIdFromUnitId(Id), Id, from, leap.Target));
        }

        internal Node[] HuntNodes(Game game)
        {
            if (!Alive || End || IsSilenced || !IsFirstAction) return Array.Empty<Node>();
            return JumpNodes(game).Prepend(Position).ToArray();
        }

        private bool CanUseOption(KnightOption option, Cooldown cooldown)
            => Alive && !End && !IsSilenced && IsHunting && OptionsUsed < KnightConsts.KnightMaxOptions
                && (int)LastOption < (int)option && cooldown.IsReady;

        internal bool CanSilence => CanUseOption(KnightOption.Silence, _silenceCooldown);

        internal Node[] CleaveNodes(Game game)
        {
            if (!CanUseOption(KnightOption.Cleave, _cleaveCooldown)) return Array.Empty<Node>();
            return game.Board.GetNodesWithinDistance(Position, KnightConsts.KnightCleaveRange,
                MapConsts.MoveManhattan, _ => true)
                .Where(entry => game.Board.GetGrid(entry.node)!.Terrain is Terrain.Ground or Terrain.Whirlpool
                    && game.Board.CanLineShot(Position, entry.node, MapConsts.DiagonalBlocked,
                        node => game.Board.GetGrid(node)!.CanShot(game)))
                .Select(entry => entry.node).ToArray();
        }

        internal Node[] CleaveRushNodes(Game game)
        {
            int playerId = game.PlayerIdFromUnitId(Id);
            return CleaveNodes(game).Where(node =>
            {
                Grid grid = game.Board.GetGrid(node)!;
                if (grid.CanStay(playerId, game)) return true;
                Building building = game.Buildings[grid.GridBuilding!.Value];
                return building.Health <= Math.Max(0, KnightConsts.KnightAttackDamage - building.HighestDirectDamage);
            }).ToArray();
        }

        internal Node[] SecretLeapNodes(Game game)
            => CanUseOption(KnightOption.SecretLeap, _secretLeapCooldown) ? JumpNodes(game) : Array.Empty<Node>();

        internal void Hunt(KnightHunt hunt, Game game)
        {
            ChangePos(hunt.Target, game);
            HuntOrigin = Position;
            IsHunting = true;
            ResolveAttack(Position, true, game);
        }

        private void UseOption(KnightOption option)
        {
            LastOption = option;
            ++OptionsUsed;
        }

        internal void Silence(Game game)
        {
            UseOption(KnightOption.Silence);
            _silenceCooldown = _silenceCooldown.Use(KnightConsts.KnightOptionCooldownTurns);
            int playerId = game.PlayerIdFromUnitId(Id);
            bool hit = false;
            foreach (Role role in game.Roles)
            {
                if (game.IsPlayerAllied(playerId, role.PlayerId)) continue;
                foreach (Unit unit in role.GetUnits())
                {
                    if (game.Board.Distance(HuntOrigin, unit.Position) is int distance
                        && distance <= KnightConsts.KnightSilenceRange && unit.TrySilence())
                        hit |= unit.ReportsSilence;
                }
            }
            game.Events.Add(KnightSilenceEvent.New(Id, HuntOrigin));
            game.Events.Add(KnightSilenceResultEvent.New(playerId, Id, hit));
        }

        internal void Cleave(Node target, bool rush, Game game)
        {
            UseOption(KnightOption.Cleave);
            _cleaveCooldown = _cleaveCooldown.Use(KnightConsts.KnightOptionCooldownTurns);
            Node from = Position;
            ResolveAttack(target, false, game);
            if (rush && Alive)
            {
                ChangePos(target, game);
                game.Events.Add(GeneralMoveEvent.New(Id, from == target ? new[] { from } : new[] { from, target }));
            }
        }

        internal void SecretLeap(KnightSecretLeap leap, Game game)
        {
            UseOption(KnightOption.SecretLeap);
            _secretLeapCooldown = _secretLeapCooldown.Use(KnightConsts.KnightOptionCooldownTurns);
            Node from = Position;
            ChangePos(leap.Target, game);
            game.Events.Add(KnightSecretLeapEvent.New(Id));
            game.Events.Add(KnightLeapResultEvent.New(game.PlayerIdFromUnitId(Id), Id, from, leap.Target));
        }

        private void ResolveAttack(Node target, bool hunt, Game game)
        {
            Grid grid = game.Board.GetGrid(target)!;
            int playerId = game.PlayerIdFromUnitId(Id);
            int? buildingId = null;
            Unit[] enemies = Array.Empty<Unit>();
            if (grid.HasBlockingBuilding(game))
            {
                Building building = game.Buildings[grid.GridBuilding!.Value];
                buildingId = building.Id;
                building.ReceiveDirectDamage(KnightConsts.KnightAttackDamage, game, playerId);
            }
            else
            {
                enemies = grid.Units.Select(id => game.GetUnit(id)!)
                    .Where(unit => unit.Alive && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unit.Id)))
                    .ToArray();
                foreach (Unit unit in enemies)
                    unit.ReceiveDirectDamage(KnightConsts.KnightAttackDamage, game, DamageType.Melee);
            }
            game.Events.Add(KnightAttackEvent.New(Id, enemies.Select(unit => unit.Id).ToArray(), buildingId, target, hunt));
        }

        public override void PrepareTurn(Game game)
        {
            base.PrepareTurn(game);
            _silenceCooldown = _silenceCooldown.Tick();
            _cleaveCooldown = _cleaveCooldown.Tick();
            _secretLeapCooldown = _secretLeapCooldown.Tick();
        }

        public override void EndRound(Game game)
        {
            base.EndRound(game);
            IsHunting = false;
            OptionsUsed = 0;
            LastOption = KnightOption.None;
        }

        public override void GeneralEnd(GeneralEnd end, Game game)
        {
            IsHunting = false;
            base.GeneralEnd(end, game);
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || End)
            {
                return false;
            }
            return !IsSilenced && IsFirstAction
                || HasOptionLeft
                || HasBuildOrDemolishTarget(game);
        }

        private bool HasOptionLeft
        {
            get
            {
                if (!IsHunting || IsSilenced || OptionsUsed >= KnightConsts.KnightMaxOptions)
                {
                    return false;
                }

                return LastOption < KnightOption.Silence && _silenceCooldown.IsReady
                    || LastOption < KnightOption.Cleave && _cleaveCooldown.IsReady
                    || LastOption < KnightOption.SecretLeap && _secretLeapCooldown.IsReady;
            }
        }

        public override UnitView GetView(Game game)
        {
            bool canBuild = Alive && !End && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);
            return KnightUnitView.New(
                Box.New(LeapNodes(game)),
                Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>()),
                Box.New(DemolishNodes(game)), HuntNodes(game), CleaveNodes(game), CleaveRushNodes(game),
                SecretLeapNodes(game), CanSilence, IsHunting,
                SilenceCooldown, CleaveCooldown, SecretLeapCooldown)!;
        }
    }

    public class KnightUnitView : UnitView
    {
        public Node[] CanLeapGrids { get; private set; }
        public Node[] CanHuntGrids { get; private set; } = Array.Empty<Node>();
        public Node[] CanCleaveGrids { get; private set; } = Array.Empty<Node>();
        public Node[] CanCleaveRushGrids { get; private set; } = Array.Empty<Node>();
        public Node[] CanSecretLeapGrids { get; private set; } = Array.Empty<Node>();
        public bool CanSilence { get; private set; }
        public bool IsHunting { get; private set; }
        public int SilenceCooldown { get; private set; }
        public int CleaveCooldown { get; private set; }
        public int SecretLeapCooldown { get; private set; }

        private KnightUnitView(Node[] canLeapGrids) : base(null!, null!, null!, null!)
            => CanLeapGrids = canLeapGrids;

        public static KnightUnitView? New(Box<Node[]> canLeapGrids, Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids, Box<Node[]> canDemolishGrids,
            Node[]? canHuntGrids = null, Node[]? canCleaveGrids = null, Node[]? canCleaveRushGrids = null,
            Node[]? canSecretLeapGrids = null, bool canSilence = false, bool isHunting = false,
            int silenceCooldown = 0, int cleaveCooldown = 0, int secretLeapCooldown = 0)
        {
            if (canLeapGrids.TryTake() is not Node[] targets)
            {
                return null;
            }
            KnightUnitView target = new(targets)
            {
                CanHuntGrids = canHuntGrids ?? Array.Empty<Node>(),
                CanCleaveGrids = canCleaveGrids ?? Array.Empty<Node>(),
                CanCleaveRushGrids = canCleaveRushGrids ?? Array.Empty<Node>(),
                CanSecretLeapGrids = canSecretLeapGrids ?? Array.Empty<Node>(),
                CanSilence = canSilence, IsHunting = isHunting, SilenceCooldown = silenceCooldown,
                CleaveCooldown = cleaveCooldown, SecretLeapCooldown = secretLeapCooldown,
            };
            return NewBase(target, Box.New(Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canBuildFortGrids, canBuildWallGrids, canDemolishGrids) ? target : null;
        }

        public override UnitView Clone()
        {
            KnightUnitView target = new((Node[])CanLeapGrids.Clone())
            {
                CanHuntGrids = (Node[])CanHuntGrids.Clone(),
                CanCleaveGrids = (Node[])CanCleaveGrids.Clone(),
                CanCleaveRushGrids = (Node[])CanCleaveRushGrids.Clone(),
                CanSecretLeapGrids = (Node[])CanSecretLeapGrids.Clone(),
                CanSilence = CanSilence, IsHunting = IsHunting, SilenceCooldown = SilenceCooldown,
                CleaveCooldown = CleaveCooldown, SecretLeapCooldown = SecretLeapCooldown,
            };
            CloneBase(target);
            return target;
        }
    }

    public class KnightLeapEvent : Event
    {
        public int UnitId { get; }
        private KnightLeapEvent(int unitId) => UnitId = unitId;
        public static KnightLeapEvent New(int unitId) => new(unitId);
    }

    public class KnightLeapResultEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public Node From { get; }
        public Node Target { get; }
        private KnightLeapResultEvent(int playerId, int unitId, Node from, Node target)
        {
            PlayerId = playerId;
            UnitId = unitId;
            From = from;
            Target = target;
        }
        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;
        public static KnightLeapResultEvent New(int playerId, int unitId, Node from, Node target)
            => new(playerId, unitId, from, target);
    }

    public class KnightAttackEvent : AttackEvent
    {
        public bool IsHunt { get; }
        private KnightAttackEvent(int unitId, int[] units, int? building, Node target, bool hunt)
            : base(unitId, units, building, target) => IsHunt = hunt;
        public static KnightAttackEvent New(int unitId, int[] units, int? building, Node target, bool hunt)
            => new(unitId, units, building, target, hunt);
    }

    public class KnightSilenceEvent : Event
    {
        public int UnitId { get; }
        public Node Center { get; }
        private KnightSilenceEvent(int unitId, Node center) { UnitId = unitId; Center = center; }
        public static KnightSilenceEvent New(int unitId, Node center) => new(unitId, center);
    }

    public class KnightSilenceResultEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public bool Hit { get; }
        private KnightSilenceResultEvent(int playerId, int unitId, bool hit)
        { PlayerId = playerId; UnitId = unitId; Hit = hit; }
        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;
        public static KnightSilenceResultEvent New(int playerId, int unitId, bool hit) => new(playerId, unitId, hit);
    }

    public class KnightSecretLeapEvent : Event
    {
        public int UnitId { get; }
        private KnightSecretLeapEvent(int unitId) => UnitId = unitId;
        public static KnightSecretLeapEvent New(int unitId) => new(unitId);
    }

    public class KnightUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is not null) return Built;
            if (Id is not int id || RoleId is not int roleId || Position is not Node position) return null;
            Built = KnightUnit.New(id, roleId, position);
            return Built;
        }
    }

    public class KnightBuilder : RoleBuilder
    {
        public Node Position { get; private set; }
        private KnightBuilder(UnitBuilder[] unitBuilder) : base(unitBuilder) { }
        public static KnightBuilder? New(UnitBuilder[] unitBuilder, Node position)
            => unitBuilder.Length == 1 && unitBuilder[0] is KnightUnitBuilder
                ? new(unitBuilder) { Position = position } : null;
        public override bool CanInitialStay(Board board)
            => board.GetGrid(Position) is Grid grid && grid.CanInitialStay();
        public override Role? Build()
        {
            if (Built is not null) return Built;
            if (Id is not int id || PlayerId is not int playerId) return null;
            UnitBuilder builder = UnitBuilder[0];
            if (builder.Position is null) builder.SetPosition(Position);
            if (builder.Position != Position || builder.Build() is not KnightUnit unit) return null;
            Built = Knight.New(id, playerId, unit);
            return Built;
        }
    }
}
