using System;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using ZLinq;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored.Roles
{
    public interface IAlphaAction { }

    public class AlphaAttack : Action, IAlphaAction
    {
        public Node Target { get; private set; }

        public static AlphaAttack New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    public class AlphaAttackRush : Action, IAlphaAction
    {
        public Node Target { get; private set; }

        public static AlphaAttackRush New(int callerUnitId, Node target)
        {
            return new() { CallerUnitId = callerUnitId, Target = target };
        }
    }

    public class AlphaAttackEvent : AttackEvent
    {
        private AlphaAttackEvent(int attackerId, int[] unitIds, int? buildingId, Node target)
            : base(attackerId, unitIds, buildingId, target) { }

        public static AlphaAttackEvent New(int attackerId, int[] unitIds, int? buildingId, Node target)
        {
            return new(attackerId, unitIds, buildingId, target);
        }
    }

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
                AlphaAttack attack => Unit.CanAttack(attack, game),
                AlphaAttackRush rush => Unit.CanAttackRush(rush, game),
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
                case AlphaAttack attack:
                    Unit.AlphaAttack(attack, game);
                    break;
                case AlphaAttackRush rush:
                    Unit.AlphaAttackRush(rush, game);
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
        public int AttackDamage => AlphaConsts.AlphaAttackDamage;

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

        internal Node[] AttackNodes(Game game)
        {
            return FarAttackNodes(game)
                .AsValueEnumerable()
                .Where(node => node != Position)
                .ToArray();
        }

        internal bool CanAttack(AlphaAttack attack, Game game)
        {
            return CanAttack(attack.Target, game);
        }

        private bool CanAttack(Node target, Game game)
        {
            return Alive && !End && MoveUsed == 0 && AttackUsed == 0
                && AttackNodes(game).AsValueEnumerable().Any(node => node == target);
        }

        internal Node[] AttackRushNodes(Game game)
        {
            int playerId = game.PlayerIdFromUnitId(Id);
            return AttackNodes(game)
                .AsValueEnumerable()
                .Where(node => CanRushStay(node, playerId, game))
                .ToArray();
        }

        internal bool CanAttackRush(AlphaAttackRush rush, Game game)
        {
            return CanAttack(rush.Target, game)
                && CanRushStay(rush.Target, game.PlayerIdFromUnitId(Id), game);
        }

        // Predict entry after the attack without changing the board during validation.
        private bool CanRushStay(Node node, int playerId, Game game)
        {
            Grid grid = game.Board.GetGrid(node)!;
            if (grid.Terrain is not (Terrain.Ground or Terrain.Whirlpool))
            {
                return false;
            }
            if (!grid.HasBlockingBuilding(game))
            {
                return grid.CanStay(playerId, game);
            }
            Building building = game.Buildings[grid.GridBuilding!.Value];
            if (game.IsPlayerAllied(playerId, building.Owner))
            {
                // Allied buildings are not damaged; entry depends on their current type.
                return grid.CanStay(playerId, game);
            }
            int damage = Math.Max(0, AttackDamage - building.HighestDirectDamage);
            return damage > 0 && building.Health <= damage;
        }

        internal void AlphaAttack(AlphaAttack attack, Game game)
        {
            ExecuteAttack(attack.Target, game);
        }

        internal void AlphaAttackRush(AlphaAttackRush rush, Game game)
        {
            Node from = Position;
            ExecuteAttack(rush.Target, game);
            if (game.Board.GetGrid(rush.Target)!.CanStay(game.PlayerIdFromUnitId(Id), game))
            {
                ChangePos(rush.Target, game);
                game.Events.Add(GeneralMoveEvent.New(Id, new[] { from, rush.Target }));
            }
        }

        private void ExecuteAttack(Node target, Game game)
        {
            ++AttackUsed;
            End = true;
            Grid grid = game.Board.GetGrid(target)!;
            int attackerPlayerId = game.PlayerIdFromUnitId(Id);
            int? attackedBuildingId = null;
            Unit[] enemies = Array.Empty<Unit>();

            // A standing building shields units even when this attack destroys it.
            if (grid.HasBlockingBuilding(game))
            {
                Building building = game.Buildings[grid.GridBuilding!.Value];
                if (!game.IsPlayerAllied(attackerPlayerId, building.Owner))
                {
                    attackedBuildingId = building.Id;
                    building.ReceiveDirectDamage(AttackDamage, game, attackerPlayerId);
                }
            }
            else
            {
                enemies = grid.Units
                    .AsValueEnumerable()
                    .Select(unitId => game.GetUnit(unitId)!)
                    .Where(unit => unit.Alive
                        && !game.IsPlayerAllied(attackerPlayerId, game.PlayerIdFromUnitId(unit.Id)))
                    .ToArray();
                foreach (Unit enemy in enemies)
                {
                    enemy.ReceiveDirectDamage(AttackDamage, game);
                }
            }
            game.Events.Add(AlphaAttackEvent.New(
                attackerId: Id,
                unitIds: enemies.AsValueEnumerable().Select(unit => unit.Id).ToArray(),
                buildingId: attackedBuildingId,
                target: target));
        }

        public override bool HasActionLeft(Game game)
        {
            if (!Alive || !base.HasActionLeft(game))
            {
                return false;
            }

            // TODO: Include Blood availability when that mechanic is added.
            AlphaUnitView view = (AlphaUnitView)GetView(game);
            return view.CanMoveGrids.Length > 0
                || view.CanAttackGrids.Length > 0
                || view.CanAttackRushGrids.Length > 0
                || view.CanBuildFortGrids.Length > 0
                || view.CanBuildWallGrids.Length > 0;
        }

        public override UnitView GetView(Game game)
        {
            bool active = Alive && !End;
            bool canMove = active && AttackUsed == 0 && MoveUsed < MoveDistance;
            bool canAttack = active && MoveUsed == 0 && AttackUsed == 0;
            bool canBuild = active && IsFirstAction
                && !game.Board.GetGrid(Position)!.HasBlockingBuilding(game);

            return AlphaUnitView.New(
                canMoveGrids: Box.New(canMove
                    ? MoveEntries(game)
                    : Array.Empty<(Node node, int distance, Node? lastNode)>()),
                canAttackGrids: Box.New(canAttack ? AttackNodes(game) : Array.Empty<Node>()),
                canAttackRushGrids: Box.New(canAttack ? AttackRushNodes(game) : Array.Empty<Node>()),
                canBuildFortGrids: Box.New(canBuild ? BuildFortNodes(game) : Array.Empty<Node>()),
                canBuildWallGrids: Box.New(canBuild ? BuildWallNodes(game) : Array.Empty<Node>())
            )!;
        }
    }

    public class AlphaUnitView : UnitView
    {
        public Node[] CanAttackGrids { get; private set; }
        public Node[] CanAttackRushGrids { get; private set; }

        private AlphaUnitView(Node[] canAttackGrids, Node[] canAttackRushGrids)
            : base(null!, null!, null!)
        {
            CanAttackGrids = canAttackGrids;
            CanAttackRushGrids = canAttackRushGrids;
        }

        public static AlphaUnitView? New(
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canAttackGrids,
            Box<Node[]> canAttackRushGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids
        )
        {
            if (canAttackGrids.TryTake() is not Node[] attackGrids)
            {
                return null;
            }
            if (canAttackRushGrids.TryTake() is not Node[] attackRushGrids)
            {
                return null;
            }
            AlphaUnitView target = new(attackGrids, attackRushGrids);
            return NewBase(target, canMoveGrids, canBuildFortGrids, canBuildWallGrids)
                ? target
                : null;
        }

        public override UnitView Clone()
        {
            AlphaUnitView target = new(
                (Node[])CanAttackGrids.Clone(), (Node[])CanAttackRushGrids.Clone());
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
