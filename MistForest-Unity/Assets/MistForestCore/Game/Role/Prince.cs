using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IPrinceAction { }

    public class PrincePlantRose : Action, IPrinceAction
    {
        public Node Target { get; private set; }

        public static PrincePlantRose New(int callerUnitId, Node target)
            => new() { CallerUnitId = callerUnitId, Target = target };
    }

    public class PrinceBloom : Action, IPrinceAction
    {
        public Node RosePosition { get; private set; }
        public Node Landing { get; private set; }
        public Node Target { get; private set; }

        public static PrinceBloom New(int callerUnitId, Node rosePosition, Node landing, Node target)
            => new() { CallerUnitId = callerUnitId, RosePosition = rosePosition, Landing = landing, Target = target };
    }

    public class Prince : Role
    {
        private readonly Unit[] _units;
        public PrinceUnit Unit => (PrinceUnit)_units[0];

        private Prince(PrinceUnit unit) => _units = new Unit[] { unit };

        internal static Prince New(int id, int playerId, PrinceUnit unit)
        {
            Prince role = new(unit);
            NewBase(role, id, playerId);
            return role;
        }

        public override Role Clone() => New(Id, PlayerId, (PrinceUnit)Unit.Clone());

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
                PrincePlantRose plant => Unit.CanPlant(plant.Target, game),
                PrinceBloom bloom => Unit.CanBloom(bloom, game),
                _ => JudgeGeneralAction(action, game),
            };
        }

        public override void ExecuteAction(Action action, Game game)
        {
            Unit.StartExecuteAction();
            switch (action)
            {
                case PrincePlantRose plant:
                    Unit.Plant(plant.Target, game);
                    break;
                case PrinceBloom bloom:
                    Unit.Bloom(bloom, game);
                    break;
                default:
                    ExecuteGeneralAction(action, game);
                    break;
            }
        }
    }

    public class PrinceUnit : Unit
    {
        private readonly List<PrinceRose> _roses = new();
        internal IReadOnlyList<PrinceRose> Roses => _roses;
        protected override int MoveDistance => PrinceConsts.PrinceMoveDistance;
        private bool CanUseSkills => Alive && !End && !IsSilenced;
        private bool HasPlantBudget => CanUseSkills && MoveUsed < MoveDistance && _roses.Count < PrinceConsts.MaxRoses;

        private PrinceUnit() { }

        internal static PrinceUnit New(int id, int roleId, Node position)
        {
            PrinceUnit unit = new() { Health = PrinceConsts.PrinceHealth };
            NewBase(unit, id, roleId, position);
            return unit;
        }

        public override Unit Clone()
        {
            PrinceUnit clone = new();
            clone._roses.AddRange(_roses.Select(rose => rose.Clone()));
            CloneBase(clone);
            return clone;
        }

        public override bool HasAction(Action action) => action is IPrinceAction;

        private static bool CanHit(Node from, Node target, Game game)
            => game.Board.GetGrid(target) is Grid { Terrain: not Terrain.Wall }
                && game.Board.CanLineShot(from, target, MapConsts.DiagonalBlocked,
                    node => game.Board.GetGrid(node)!.CanShot(game));

        internal bool CanPlant(Node target, Game game)
            => HasPlantBudget && game.Board.Distance(Position, target) is <= PrinceConsts.PlantRange
                && game.FindRose(target) is null && CanHit(Position, target, game);

        private Node[] PlantTargets(Game game)
        {
            if (!HasPlantBudget)
            {
                return Array.Empty<Node>();
            }
            return game.Board.GetNodesWithinDistance(Position, PrinceConsts.PlantRange, MapConsts.MoveManhattan, _ => true)
                .Select(entry => entry.node)
                .Where(target => CanPlant(target, game))
                .ToArray();
        }

        internal void Plant(Node target, Game game)
        {
            MoveUsed += MoveDistance;
            _roses.Add(new PrinceRose(Id, target));
            var (unitIds, buildingId) = DamageTarget(target, PrinceConsts.PlantDamage, game);
            game.Events.Add(PrincePlantRoseEvent.New(Id, unitIds, buildingId, target));
        }

        private static IEnumerable<Node> AdjacentAndSelf(Node center, Board board)
        {
            yield return center;
            foreach ((int dx, int dy) in MapConsts.MoveManhattan)
            {
                if (board.Move(center, dx, dy) is Node neighbor)
                {
                    yield return neighbor;
                }
            }
        }

        private bool CanLand(PrinceRose rose, Node landing, Game game)
            => !rose.UsedThisTurn
                && game.Board.Distance(rose.Position, landing) is <= PrinceConsts.BloomLandingRange
                && game.Board.GetGrid(landing)!.CanStay(game.PlayerIdFromUnitId(Id), game);

        private static bool CanBloomHit(Node landing, Node target, Game game)
            => game.Board.Distance(landing, target) is <= PrinceConsts.BloomAttackRange
                && CanHit(landing, target, game);

        private (Node rose, Node landing, Node[] attackGrids)[] BloomTargets(Game game)
        {
            if (!CanUseSkills)
            {
                return Array.Empty<(Node, Node, Node[])>();
            }

            List<(Node rose, Node landing, Node[] attackGrids)> targets = new();
            foreach (PrinceRose rose in _roses)
            {
                foreach (Node landing in AdjacentAndSelf(rose.Position, game.Board))
                {
                    if (CanLand(rose, landing, game))
                    {
                        Node[] attackGrids = AdjacentAndSelf(landing, game.Board)
                            .Where(target => CanBloomHit(landing, target, game))
                            .ToArray();
                        targets.Add((rose.Position, landing, attackGrids));
                    }
                }
            }
            return targets.ToArray();
        }

        internal bool CanBloom(PrinceBloom bloom, Game game)
            => CanUseSkills && _roses.Any(rose => rose.Position == bloom.RosePosition && CanLand(rose, bloom.Landing, game))
                && CanBloomHit(bloom.Landing, bloom.Target, game);

        internal void Bloom(PrinceBloom bloom, Game game)
        {
            PrinceRose rose = _roses.First(entry => entry.Position == bloom.RosePosition);
            rose.UseBloom();
            if (rose.Health == 0)
            {
                _roses.Remove(rose);
            }

            Node from = Position;
            if (from != bloom.Landing)
            {
                ChangePos(bloom.Landing, game, blink: true);
            }
            var (unitIds, buildingId) = DamageTarget(bloom.Target, PrinceConsts.BloomDamage, game);
            game.Events.Add(PrinceBloomEvent.New(Id, unitIds, buildingId, bloom.Target, rose.Position, rose.Health));
            game.Events.Add(PrinceBlinkEvent.New(game.PlayerIdFromUnitId(Id), Id, from, bloom.Landing));
        }

        private (int[] unitIds, int? buildingId) DamageTarget(Node target, int damage, Game game)
        {
            game.DamageRoseAt(Id, target, damage);
            int playerId = game.PlayerIdFromUnitId(Id);
            Grid grid = game.Board.GetGrid(target)!;
            if (grid.HasBlockingBuilding(game))
            {
                int buildingId = grid.GridBuilding!.Value;
                game.Buildings[buildingId].ReceiveDirectDamage(damage, game, playerId);
                return (Array.Empty<int>(), buildingId);
            }

            List<int> unitIds = new();
            foreach (int unitId in grid.Units.ToArray())
            {
                Unit unit = game.GetUnit(unitId)!;
                if (unit.Alive && !game.IsPlayerAllied(playerId, game.PlayerIdFromUnitId(unitId)))
                {
                    unit.ReceiveDirectDamage(damage, game, DamageType.Melee);
                    unitIds.Add(unitId);
                }
            }
            return (unitIds.ToArray(), null);
        }

        internal void RemoveRose(PrinceRose rose) => _roses.Remove(rose);

        public override void EndTurn(Game game)
        {
            base.EndTurn(game);
            foreach (PrinceRose rose in _roses)
            {
                rose.EndTurn();
            }
        }

        public override bool HasActionLeft(Game game)
            => Alive && !End && (MoveUsed < MoveDistance && MoveEntries(game).Length > 0
                || PlantTargets(game).Length > 0 || BloomTargets(game).Length > 0 || HasBuildOrDemolishTarget(game));

        public override UnitView GetView(Game game)
        {
            bool canBuild = Alive && !End && CanBuildCommon(game);
            return new PrinceUnitView(
                Alive && !End && MoveUsed < MoveDistance ? MoveEntries(game) : Array.Empty<(Node, int, Node?)>(),
                canBuild ? BuildFortNodes(game) : Array.Empty<Node>(),
                canBuild ? BuildWallNodes(game) : Array.Empty<Node>(),
                DemolishNodes(game),
                PlantTargets(game),
                BloomTargets(game),
                _roses.Select(rose => (rose.Position, rose.Health, rose.UsedThisTurn)).ToArray());
        }
    }

    public sealed class PrinceRose
    {
        public int UnitId { get; }
        public Node Position { get; }
        public int Health { get; private set; } = PrinceConsts.RoseHealth;
        public bool UsedThisTurn { get; private set; }
        public int HighestDirectDamage { get; private set; }

        internal PrinceRose(int unitId, Node position)
        {
            UnitId = unitId;
            Position = position;
        }

        internal PrinceRose Clone() => new(UnitId, Position)
        {
            Health = Health,
            UsedThisTurn = UsedThisTurn,
            HighestDirectDamage = HighestDirectDamage,
        };

        internal int ReceiveDirectDamage(int strength)
        {
            int damage = Math.Max(0, strength - HighestDirectDamage);
            HighestDirectDamage = Math.Max(HighestDirectDamage, strength);
            return ApplyDamage(damage);
        }

        internal void UseBloom()
        {
            UsedThisTurn = true;
            ApplyDamage(PrinceConsts.BloomRoseCost);
        }

        internal int ApplyDamage(int damage)
        {
            int actualDamage = Math.Min(Health, damage);
            Health -= actualDamage;
            return actualDamage;
        }

        internal void EndTurn() => UsedThisTurn = false;
        internal void EndRound() => HighestDirectDamage = 0;
    }

    public class PrinceUnitView : UnitView
    {
        public Node[] CanPlantGrids { get; }
        public (Node rose, Node landing, Node[] attackGrids)[] CanBloomGrids { get; }
        public (Node position, int health, bool usedThisTurn)[] Roses { get; }

        internal PrinceUnitView(
            (Node node, int distance, Node? lastNode)[] moveGrids, Node[] buildFortGrids,
            Node[] buildWallGrids, Node[] demolishGrids, Node[] plantGrids,
            (Node rose, Node landing, Node[] attackGrids)[] bloomGrids,
            (Node position, int health, bool usedThisTurn)[] roses)
            : base(moveGrids, buildFortGrids, buildWallGrids, demolishGrids)
        {
            CanPlantGrids = plantGrids;
            CanBloomGrids = bloomGrids;
            Roses = roses;
        }

        public override UnitView Clone()
        {
            PrinceUnitView clone = new(CanMoveGrids, CanBuildFortGrids, CanBuildWallGrids, CanDemolishGrids,
                (Node[])CanPlantGrids.Clone(),
                CanBloomGrids.Select(entry => (entry.rose, entry.landing, (Node[])entry.attackGrids.Clone())).ToArray(),
                ((Node, int, bool)[])Roses.Clone());
            CloneBase(clone);
            return clone;
        }
    }

    public class PrincePlantRoseEvent : AttackEvent
    {
        private PrincePlantRoseEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static PrincePlantRoseEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
            => new(attackerId, unitIds, buildingId, target);
    }

    public class PrinceBloomEvent : AttackEvent
    {
        public Node RosePosition { get; }
        public int RoseHealth { get; }

        private PrinceBloomEvent(int attackerId, int[] unitIds, int? buildingId, Node target, Node rosePosition, int roseHealth)
            : base(attackerId, unitIds, buildingId, target)
        {
            RosePosition = rosePosition;
            RoseHealth = roseHealth;
        }

        public static PrinceBloomEvent New(
            int attackerId, int[] unitIds, int? buildingId, Node target, Node rosePosition, int roseHealth)
            => new(attackerId, unitIds, buildingId, target, rosePosition, roseHealth);
    }

    public class PrinceBlinkEvent : Event
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public Node From { get; }
        public Node Target { get; }

        private PrinceBlinkEvent(int playerId, int unitId, Node from, Node target)
        {
            PlayerId = playerId;
            UnitId = unitId;
            From = from;
            Target = target;
        }

        public static PrinceBlinkEvent New(int playerId, int unitId, Node from, Node target)
            => new(playerId, unitId, from, target);

        public override bool IsVisibleTo(int playerId) => playerId == PlayerId;
    }

    public class PrinceRoseDamageEvent : Event
    {
        public int AttackerId { get; }
        public int RoseOwnerUnitId { get; }
        public Node Target { get; }
        public int RemainingHealth { get; }

        private PrinceRoseDamageEvent(int attackerId, int roseOwnerUnitId, Node target, int remainingHealth)
        {
            AttackerId = attackerId;
            RoseOwnerUnitId = roseOwnerUnitId;
            Target = target;
            RemainingHealth = remainingHealth;
        }

        public static PrinceRoseDamageEvent New(int attackerId, int roseOwnerUnitId, Node target, int remainingHealth)
            => new(attackerId, roseOwnerUnitId, target, remainingHealth);
    }

    public class PrinceUnitBuilder : UnitBuilder
    {
        public override Unit? Build()
        {
            if (Built is null && Id is int id && RoleId is int roleId && Position is Node position)
            {
                Built = PrinceUnit.New(id, roleId, position);
            }
            return Built;
        }
    }

    public class PrinceBuilder : RoleBuilder
    {
        public Node Position { get; }

        private PrinceBuilder(UnitBuilder[] unitBuilder, Node position) : base(unitBuilder)
            => Position = position;

        public static PrinceBuilder? New(UnitBuilder[] unitBuilder, Node position)
            => unitBuilder.Length == 1 && unitBuilder[0] is PrinceUnitBuilder
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
            if (builder.Position != Position || builder.Build() is not PrinceUnit unit)
            {
                return null;
            }
            Built = Prince.New(id, playerId, unit);
            return Built;
        }
    }
}
