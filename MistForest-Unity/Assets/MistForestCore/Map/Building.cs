using MistForest.Core.Consts;
using MistForest.Core.GameRefactored;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameMap
{
    public abstract class Building
    {
        public int Id;
        public Node Pos;
        public int Owner;
        public int Health;

        protected Building() { }

        protected static void NewBase(Building target, int id, Node pos, int owner)
        {
            target.Id = id;
            target.Pos = pos;
            target.Owner = owner;
        }

        public virtual void Destroyed(Game game, int destroyer)
        {
            Health = 0;
            Rubble rubble = Rubble.New(Id, Pos, owner: destroyer);
            game.Buildings[Id] = rubble;
        }

        public class Fort : Building
        {
            public static Fort New(int id, Node pos, int owner, int health)
            {
                Fort fort = new() { Health = health };
                NewBase(fort, id, pos, owner);
                return fort;
            }
        }

        public class Wall : Building
        {
            public static Wall New(int id, Node pos, int owner, int health)
            {
                Wall wall = new() { Health = health };
                NewBase(wall, id, pos, owner);
                return wall;
            }
        }

        public class GreatWall : Building { }

        public class Rubble : Building
        {
            int expireRound;

            public static Rubble New(int id, Node pos, int owner)
            {
                Rubble rubble = new() { expireRound = MapConsts.RubbleExpireRound, Health = 0 };
                NewBase(rubble, id, pos, owner);
                return rubble;
            }

            public override void Destroyed(Game game, int _)
            {
                game.Board.GetGrid(Pos)!.GridBuilding = null;
            }

            public void PrepareTurn(Game game)
            {
                --expireRound;
                if (expireRound == 0)
                {
                    Destroyed(game, 0);
                }
            }
        }
    }

    public abstract class BuildingBuilder
    {
        protected int? _id;
        protected Node? _pos;
        protected int? _owner;
        protected int? _health;

        public BuildingBuilder? SetId(int id)
        {
            if (_id is not null)
            {
                return null;
            }
            _id = id;
            return this;
        }

        public BuildingBuilder? SetPos(Node pos)
        {
            if (_pos is not null)
            {
                return null;
            }
            _pos = pos;
            return this;
        }

        public BuildingBuilder? SetOwner(int owner)
        {
            if (_owner is not null)
            {
                return null;
            }
            _owner = owner;
            return this;
        }

        public BuildingBuilder? SetHealth(int health)
        {
            if (_health is not null)
            {
                return null;
            }
            _health = health;
            return this;
        }

        public abstract Building? Build();
    }

    public class FortBuilder : BuildingBuilder
    {
        public static FortBuilder New() => new();

        public override Building? Build()
        {
            if (
                _id is not int id
                || _pos is not Node pos
                || _owner is not int owner
                || _health is not int health
            )
            {
                return null;
            }
            return Building.Fort.New(id, pos, owner, health);
        }
    }

    public class WallBuilder : BuildingBuilder
    {
        public static WallBuilder New() => new();

        public override Building? Build()
        {
            if (
                _id is not int id
                || _pos is not Node pos
                || _owner is not int owner
                || _health is not int health
            )
            {
                return null;
            }
            return Building.Wall.New(id, pos, owner, health);
        }
    }
}
