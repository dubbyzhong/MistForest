using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public abstract class Role : IClone<Role>
    {
        public int Id { get; internal set; }
        public int PlayerId { get; internal set; }
        protected bool End { get; set; }
        public bool Alive => GetUnits().Any(unit => unit.Alive);

        public virtual void GeneralEnd(Game game)
        {
            End = true;
            game.Events.Add(GeneralEndEvent.New(unitId: Id));
        }

        public virtual void GeneralSurrender(Game game)
        {
            End = true;
            foreach (Unit unit in GetUnits())
            {
                unit.Killed(game);
            }
            game.Events.Add(GeneralSurrenderEvent.New(unitId: Id));
        }

        public abstract Role Clone();
        public abstract bool HasActionLeft();
        public abstract Unit[] GetUnits();
    }

    public abstract class Unit : IClone<Unit>
    {
        public int Id { get; internal set; }
        public int RoleId { get; internal set; }
        public virtual int Health { get; protected set; }
        public Node Position { get; set; }
        protected (Node node, int distance, Node? lastNode)[]? _lastTargetNodes;
        protected int MoveUsed { get; set; }
        protected virtual int MoveDistance { get; set; }
        public bool Alive => Health > 0;
        public int WhirlpoolImmuneExpire { get; protected set; }

        protected Unit() { }

        protected Unit(Node position) => Position = position;

        protected static Unit NewBase(Unit target, int Id, int roleId, Node position)
        {
            target.Id = Id;
            target.RoleId = roleId;
            target.Position = position;
            target._lastTargetNodes = null;
            target.MoveUsed = 0;
            target.WhirlpoolImmuneExpire = 0;
            return target;
        }

        public abstract Unit Clone();

        protected Unit CloneBase(Unit target)
        {
            target.Id = Id;
            target.RoleId = RoleId;
            target.Health = Health;
            target.Position = Position;
            target._lastTargetNodes = null;
            target.MoveUsed = MoveUsed;
            target.MoveDistance = MoveDistance;
            return target;
        }

        internal virtual bool CanMove(GeneralMove move, Game game)
        {
            if (MoveDistance - MoveUsed <= 0)
            {
                return false;
            }
            _lastTargetNodes ??= game.Board.GetNodesWithinDistance(
                Position,
                MoveDistance - MoveUsed,
                game.CurrentPlayerId,
                game
            );
            if (_lastTargetNodes!.Any(node => node.node == move.Target))
            {
                return true;
            }
            return false;
        }

        internal virtual bool CanEnd()
        {
            return true;
        }

        internal virtual bool CanSurrender()
        {
            return true;
        }

        internal void GeneralMove(GeneralMove move, Game game)
        {
            var (_, distance, _) = _lastTargetNodes!.First(item => item.node == move.Target);
            Node[] path = GetPath(move.Target);
            MoveUsed += distance;
            ChangePos(move.Target, game);
            game.Events.Add(GeneralMoveEvent.New(Id, path));
        }

        private void WhirlpoolAttach(Game game)
        {
            _lastTargetNodes ??= game.Board.GetNodesWithinDistance(
                Position,
                game.CurrentPlayerId,
                MapConsts.WhirlAttachDistance,
                game
            );
            int lastNodeId = 0;
            foreach (int distance in Enumerable.Range(0, MapConsts.WhirlAttachDistance + 1))
            {
                var nodes = _lastTargetNodes!
                    [lastNodeId..]
                    .Where(item => item.distance == distance)
                    .ToArray();
                Node[] filteredNodes = nodes
                    .Where(item =>
                        game.Board.GetGrid(item.node)!.Terrain is GameMap.Terrain.Whirlpool
                    )
                    .Select(item => item.node)
                    .ToArray();
                if (filteredNodes.Length > 0)
                {
                    int randomIndex = game.Board.Random.NextInt(0, filteredNodes.Length)!.Value;
                    ChangePos(target: filteredNodes[randomIndex], game);
                    game.Events.Add(
                        WhirlpoolAttachEvent.New(Id, GetPath(filteredNodes[randomIndex]))
                    );
                    WhirlpoolImmuneExpire = MapConsts.WhirlpoolImmuneExpire;
                    return;
                }
                lastNodeId += nodes.Length;
            }
        }

        protected void EndRoundBase(Game game)
        {
            if (WhirlpoolImmuneExpire == 0)
            {
                WhirlpoolAttach(game);
            }
            --WhirlpoolImmuneExpire;
        }

        internal void ChangePos(Node target, Game game)
        {
            _lastTargetNodes = null;
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
                current = _lastTargetNodes!.First(item => item.node == current).lastNode;
            }
            path.Reverse();
            return path.ToArray();
        }

        internal void Killed(Game game)
        {
            Health = 0;
            _lastTargetNodes = null;
            game.Board.GetGrid(Position)!.Units.Remove(Id);
        }

        public abstract bool HasActionLeft();
        public abstract void EndRound(Game game);
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
