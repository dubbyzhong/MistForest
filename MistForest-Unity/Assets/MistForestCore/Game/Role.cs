using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Utils;
using ZLinq;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Role : IClone<Role>
    {
        public int Id { get; internal set; }
        public int PlayerId { get; internal set; }
        public bool Alive => GetUnits().AsValueEnumerable().Any(unit => unit.Alive);
        public bool Acted =>
            GetUnits().AsValueEnumerable().Where(unit => unit.Alive).Any(unit => unit.Acted);

        protected static void NewBase(Role target, int Id, int playerId)
        {
            target.Id = Id;
            target.PlayerId = playerId;
        }

        protected bool End(Game game)
        {
            return GetUnits()
                .AsValueEnumerable()
                .Where(unit => unit.Alive)
                .All(unit => unit.HasActionLeft(game) is false);
        }

        public virtual void GeneralSurrender(Game game)
        {
            foreach (Role role in game.Players[PlayerId].Roles.AsValueEnumerable())
            {
                foreach (Unit unit in role.GetUnits())
                {
                    unit.Killed(game);
                }
            }
            game.Events.Add(GeneralSurrenderEvent.New(unitId: Id));
        }

        public abstract Role Clone();
        public abstract bool HasActionLeft(Game game);
        public abstract ReadOnlySpan<Unit> GetUnits();
        public abstract void ExecuteAction(Action action, Game game);
        public abstract bool JudgeAction(Action action, Game game);
    }

    public abstract class Unit : IClone<Unit>
    {
        public int Id { get; internal set; }
        public int RoleId { get; internal set; }
        public virtual int Health { get; protected set; }
        public Node Position { get; set; }
        protected (Node node, int distance, Node? lastNode)[]? _lastMoveEntries;
        protected int MoveUsed { get; set; }
        protected int AttackUsed { get; set; }
        protected virtual int MoveDistance { get; set; }
        public bool Alive => Health > 0;
        public int WhirlpoolImmuneExpire { get; protected set; }
        public bool End { get; protected set; }
        public bool Acted { get; protected set; }
        public int HighestDirectDamage { get; private set; }

        protected Unit() { }

        protected Unit(Node position) => Position = position;

        protected static Unit NewBase(Unit target, int Id, int roleId, Node position)
        {
            target.Id = Id;
            target.RoleId = roleId;
            target.Position = position;
            target._lastMoveEntries = null;
            target.MoveUsed = 0;
            target.AttackUsed = 0;
            target.WhirlpoolImmuneExpire = 0;
            return target;
        }

        public abstract Unit Clone();

        protected void CloneBase(Unit target)
        {
            target.Id = Id;
            target.RoleId = RoleId;
            target.Health = Health;
            target.Position = Position;
            target._lastMoveEntries = null;
            target.MoveUsed = MoveUsed;
            target.MoveDistance = MoveDistance;
            target.End = End;
            target.Acted = Acted;
            target.WhirlpoolImmuneExpire = WhirlpoolImmuneExpire;
        }

        protected virtual Func<Node, bool> MovePredicate(Game game)
        {
            return to => game.Board.GetGrid(to)!.CanStay(game.CurrentPlayerId, game);
        }

        protected virtual (Node node, int distance, Node? lastNode)[] MoveEntries(Game game)
        {
            return game.Board.GetNodesWithinDistance(
                Position,
                MoveDistance - MoveUsed,
                moveDirs: MapConsts.MoveManhattan,
                predicate: MovePredicate(game)
            );
        }

        internal virtual bool CanMove(GeneralMove move, Game game)
        {
            if (MoveDistance - MoveUsed <= 0)
            {
                return false;
            }
            _lastMoveEntries = MoveEntries(game);

            return _lastMoveEntries!
                .AsSpan()
                .AsValueEnumerable()
                .Any(entry => entry.node == move.Target);
        }

        internal virtual bool CanEnd()
        {
            return true;
        }

        internal virtual bool CanSurrender()
        {
            return true;
        }

        protected Node[] BuildFortNodes(Game game)
        {
            return game
                .Board.GetNodesWithinDistance(
                    from: Position,
                    maxDistance: RoleConsts.MaxFortBuildRange,
                    moveDirs: MapConsts.MoveManhattan,
                    predicate: _ => true
                )
                .AsValueEnumerable()
                .Where(entry =>
                    entry.distance > 0
                    && game.Board.GetGrid(entry.node)!.CanBuild()
                    && game.Board.CanLineShot(
                        Position,
                        entry.node,
                        MapConsts.DiagonalBlocked,
                        predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                    )
                )
                .Select(entry => entry.node)
                .ToArray();
        }

        private bool CanBuildCommon(Game game)
        {
            if (Acted)
            {
                return false;
            }

            if (
                game.Board.GetGrid(Position)!.GridBuilding is int buildingId
                && game.Buildings[buildingId] is not Building.Rubble
            )
            {
                return false;
            }

            return true;
        }

        internal virtual bool CanBuildFort(GeneralBuildFort buildFort, Game game)
        {
            if (CanBuildCommon(game) is false)
            {
                return false;
            }

            if (game.Board.GetGrid(buildFort.Target)!.CanBuild() is false)
            {
                return false;
            }

            int distance = game.Board.Distance(Position, buildFort.Target)!.Value;

            if (distance > RoleConsts.MaxFortBuildRange || distance < 1)
            {
                return false;
            }

            return game.Board.CanLineShot(
                Position,
                buildFort.Target,
                MapConsts.DiagonalBlocked,
                predicate: node => game.Board.GetGrid(node)!.CanShot(game)
            );
        }

        protected Node[] BuildWallNodes(Game game)
        {
            return game
                .Board.GetNodesWithinDistance(
                    from: Position,
                    maxDistance: RoleConsts.MaxWallBuildRange,
                    moveDirs: MapConsts.MoveChebyshev,
                    predicate: _ => true
                )
                .AsValueEnumerable()
                .Where(entry =>
                    entry.distance > 0
                    && game.Board.GetGrid(entry.node)!.CanBuild()
                    && game.Board.CanLineShot(
                        Position,
                        entry.node,
                        MapConsts.DiagonalBlocked,
                        predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                    )
                )
                .Select(entry => entry.node)
                .ToArray();
        }

        internal virtual bool CanBuildWall(GeneralBuildWall buildWall, Game game)
        {
            if (CanBuildCommon(game) is false)
            {
                return false;
            }

            if (
                buildWall
                    .Targets.AsSpan()
                    .AsValueEnumerable()
                    .Any(target => game.Board.GetGrid(target)!.CanBuild() is false)
            )
            {
                return false;
            }

            Node[] wallNodes = BuildWallNodes(game);

            Bitset visited = Bitset.WithCapacity(buildWall.Targets.Length);
            Queue<int> queue = new();
            queue.Enqueue(0);
            int nodeCount = 0;
            while (queue.Count > 0)
            {
                int currentNodeId = queue.Dequeue();
                Node currentNode = buildWall.Targets[currentNodeId];
                ++nodeCount;
                foreach ((int dx, int dy) in MapConsts.MoveManhattan)
                {
                    if (game.Board.Move(currentNode, dx, dy) is not Node to)
                    {
                        continue;
                    }
                    if (
                        buildWall
                            .Targets.AsValueEnumerable()
                            .WithIndex()
                            .FirstOrDefault(
                                targetWithIndex => targetWithIndex.item == to,
                                defaultValue: (default, -1)
                            )
                            is (_, int toId)
                        && toId == -1
                    )
                    {
                        continue;
                    }
                    if (visited[toId])
                    {
                        continue;
                    }
                    queue.Enqueue(toId);
                    visited[toId] = true;
                }
            }
            if (nodeCount < buildWall.Targets.Length)
            {
                return false;
            }
            return buildWall
                .Targets.AsSpan()
                .AsValueEnumerable()
                .All(target => wallNodes.AsSpan().AsValueEnumerable().Any(node => node == target));
        }

        internal bool CanDemolish(GeneralDemolish demolish, Game game)
        {
            if (Acted)
            {
                return false;
            }
            if (game.Board.Distance(Position, demolish.Target) > RoleConsts.MaxDemolishRange)
            {
                return false;
            }
            return game.Board.CanLineShot(
                    Position,
                    demolish.Target,
                    MapConsts.DiagonalBlocked,
                    predicate: node => game.Board.GetGrid(node)!.CanShot(game)
                )
                && game.Board.GetGrid(demolish.Target)?.GridBuilding is int buildingId
                && game.Buildings[buildingId] is not Building.Rubble
                && game.IsPlayerAllied(
                    game.Buildings[buildingId].Owner,
                    game.PlayerIdFromUnitId(Id)
                );
        }

        internal void StartExecuteAction()
        {
            Acted = true;
        }

        internal void GeneralMove(GeneralMove move, Game game)
        {
            var (_, distance, _) = _lastMoveEntries!
                .AsSpan()
                .AsValueEnumerable()
                .First(item => item.node == move.Target);
            Node[] path = GetPath(move.Target);
            MoveUsed += distance;
            ChangePos(move.Target, game);
            game.Events.Add(GeneralMoveEvent.New(Id, path));
        }

        public virtual void GeneralEnd(GeneralEnd end, Game game)
        {
            End = true;
            game.Events.Add(GeneralEndEvent.New(unitId: Id));
        }

        internal void GeneralBuildFort(GeneralBuildFort buildFort, Game game)
        {
            End = true;
            if (game.Board.GetGrid(buildFort.Target)!.Units.Count > 0)
            {
                game.Events.Add(
                    GeneralBuildFortEvent.New(unitId: Id, target: buildFort.Target, success: false)
                );
                return;
            }
            game.AddBuilding(
                FortBuilder
                    .New()
                    .SetHealth(MapConsts.FortHealth)!
                    .SetOwner(game.PlayerIdFromUnitId(Id))!
                    .SetPos(buildFort.Target)!
            );
            game.Events.Add(
                GeneralBuildFortEvent.New(unitId: Id, target: buildFort.Target, success: true)
            );
        }

        internal void GeneralBuildWall(GeneralBuildWall buildWall, Game game)
        {
            End = true;
            if (
                buildWall
                    .Targets.AsSpan()
                    .AsValueEnumerable()
                    .Any(target => game.Board.GetGrid(target)!.Units.Count > 0)
            )
            {
                game.Events.Add(
                    GeneralBuildWallEvent.New(
                        unitId: Id,
                        targets: buildWall.Targets,
                        success: false
                    )
                );
                return;
            }
            foreach (Node target in buildWall.Targets)
            {
                game.AddBuilding(
                    WallBuilder
                        .New()
                        .SetHealth(MapConsts.WallHealth)!
                        .SetOwner(game.PlayerIdFromUnitId(Id))!
                        .SetPos(target)!
                );
            }
            game.Events.Add(
                GeneralBuildWallEvent.New(unitId: Id, targets: buildWall.Targets, success: true)
            );
        }

        internal void GeneralDemolish(GeneralDemolish demolish, Game game)
        {
            End = true;
            int buildingId = game.Board.GetGrid(demolish.Target)!.GridBuilding!.Value;
            game.Buildings[buildingId].Destroyed(game, game.PlayerIdFromUnitId(Id));
            game.Events.Add(GeneralDemolishEvent.New(unitId: Id, target: demolish.Target));
        }
        internal void ReceiveDirectDamage(int strength, Game game)
        {
            if (!Alive || strength <= HighestDirectDamage)
            {
                return;
            }

            int damage = strength - HighestDirectDamage;
            HighestDirectDamage = strength;

            Health = System.Math.Max(0, Health - damage);

            if (Health == 0)
            {
                Killed(game);
            }
        }

        private void WhirlpoolAttach(Game game)
        {
            _lastMoveEntries = game.Board.GetNodesWithinDistance(
                Position,
                MapConsts.WhirlAttachDistance,
                moveDirs: MapConsts.MoveManhattan,
                predicate: MovePredicate(game)
            );
            int lastNodeId = 0;
            for (int distance = 0; distance <= MapConsts.WhirlAttachDistance; ++distance)
            {
                List<Node> filteredNodes = new();
                while (
                    lastNodeId < _lastMoveEntries.Length
                    && _lastMoveEntries[lastNodeId].distance == distance
                )
                {
                    (Node node, _, _) = _lastMoveEntries[lastNodeId]!;
                    if (game.Board.GetGrid(node)!.Terrain is Terrain.Whirlpool)
                    {
                        filteredNodes.Add(node);
                    }
                    ++lastNodeId;
                }
                if (filteredNodes.Count > 0)
                {
                    int randomIndex = game.Board.Random.NextInt(0, filteredNodes.Count)!.Value;
                    game.Events.Add(
                        WhirlpoolAttachEvent.New(Id, GetPath(filteredNodes[randomIndex]))
                    );
                    ChangePos(target: filteredNodes[randomIndex], game);
                    WhirlpoolImmuneExpire = MapConsts.WhirlpoolImmuneExpire;
                    return;
                }
            }
        }

        public virtual void EndRound(Game game)
        {
            Acted = false;
            End = false;
            MoveUsed = 0;
            AttackUsed = 0;
            if (WhirlpoolImmuneExpire == 0)
            {
                WhirlpoolAttach(game);
            }
            else
            {
                --WhirlpoolImmuneExpire;
            }
        }

        internal void ChangePos(Node target, Game game)
        {
            _lastMoveEntries = null;
            game.Board.GetGrid(Position)!.Units.Remove(Id);
            Position = target;
            game.Board.GetGrid(target)!.Units.Add(Id);
        }

        internal Node[] GetPath(Node target)
        {
            var path = new List<Node>();
            Node? current = target;
            while (current != null)
            {
                path.Add(current!.Value);
                current = _lastMoveEntries!
                    .AsSpan()
                    .AsValueEnumerable()
                    .First(item => item.node == current)
                    .lastNode;
            }
            path.Reverse();
            return path.ToArray();
        }

        internal void Killed(Game game)
        {
            Health = 0;
            _lastMoveEntries = null;
            game.Board.GetGrid(Position)!.Units.Remove(Id);
        }

        public virtual bool HasActionLeft(Game game)
        {
            return End is false;
        }

        public abstract UnitView GetView(Game game);
        public abstract bool HasAction(Action action);
    }

    public abstract class UnitView : IClone<UnitView>
    {
        public (Node node, int distance, Node? lastNode)[] CanMoveGrids;
        public Node[] CanBuildFortGrids;
        public Node[] CanBuildWallGrids;

        protected UnitView(
            (Node node, int distance, Node? lastNode)[] canMoveGrids,
            Node[] canBuildFortGrids,
            Node[] canBuildWallGrids
        )
        {
            CanMoveGrids = canMoveGrids;
            CanBuildFortGrids = canBuildFortGrids;
            CanBuildWallGrids = canBuildWallGrids;
        }

        public static bool NewBase(
            UnitView target,
            Box<(Node node, int distance, Node? lastNode)[]> canMoveGrids,
            Box<Node[]> canBuildFortGrids,
            Box<Node[]> canBuildWallGrids
        )
        {
            if (
                canMoveGrids.TryTake()
                is not (Node node, int distance, Node? lastNode)[] canMoveGridsValue
            )
            {
                return false;
            }
            if (canBuildFortGrids.TryTake() is not Node[] canBuildFortGridsValue)
            {
                return false;
            }
            if (canBuildWallGrids.TryTake() is not Node[] canBuildWallGridsValue)
            {
                return false;
            }
            target.CanMoveGrids = canMoveGridsValue;
            target.CanBuildFortGrids = canBuildFortGridsValue;
            target.CanBuildWallGrids = canBuildWallGridsValue;
            return true;
        }

        public void CloneBase(UnitView target)
        {
            target.CanMoveGrids = ((Node node, int distance, Node? lastNode)[])CanMoveGrids.Clone();
            target.CanBuildFortGrids = (Node[])CanBuildFortGrids.Clone();
            target.CanBuildWallGrids = (Node[])CanBuildWallGrids.Clone();
        }

        public abstract UnitView Clone();
    }

    public abstract class UnitBuilder
    {
        public int? Id { get; internal set; }
        public int? RoleId { get; internal set; }
        public Node? Position { get; private set; }

        public Unit? Built;

        public UnitBuilder? SetId(int id)
        {
            if (Id is not null)
            {
                return null;
            }
            Id = id;
            return this;
        }

        public UnitBuilder? SetRoleId(int roleId)
        {
            if (RoleId is not null)
            {
                return null;
            }
            RoleId = roleId;
            return this;
        }

        public UnitBuilder? SetPosition(Node position)
        {
            if (Position is not null)
            {
                return null;
            }
            Position = position;
            return this;
        }

        public abstract Unit? Build();
    }
}
