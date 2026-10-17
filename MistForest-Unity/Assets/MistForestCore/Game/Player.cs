using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using static MistForest.Core.GameMap.Board;

namespace MistForest.Core.GameRefactored
{
    public class Player : IClone<Player>
    {
        // public int Id { get; internal set; }
        public int? TeamId { get; internal set; }

        public readonly Role[] Roles;

        private Player(Role[] Roles) => this.Roles = Roles;

        public static Player New(Role[] Roles)
        {
            return new Player(Roles);
        }

        public Player Clone()
        {
            return new(Roles.Select(role => role.Clone()).ToArray()) { TeamId = TeamId };
        }
    }

    public class PlayerBuilder
    {
        public int? TeamId { get; set; }
        public List<RoleBuilder> Roles { get; private set; }

        private PlayerBuilder(List<RoleBuilder> roles) => Roles = roles;

        public Player Build()
        {
            return Player.New(Roles.Select(role => role.Build()).ToArray());
        }

        public static PlayerBuilder? New(List<RoleBuilder> roles)
        {
            if (roles.Count() > PlayerConsts.MaxRolesPerPlayer)
            {
                return null;
            }
            if (roles.Distinct(RoleComparerByType.Instance).Count() != roles.Count())
            {
                return null;
            }
            return new(roles);
        }

        public abstract class RoleBuilder
        {
            public abstract bool CanInitialStay(Board board);
            public abstract Role Build();

            public class SniperBuilder : RoleBuilder
            {
                public Node Position { get; private set; }

                private SniperBuilder(Node position) => Position = position;

                public static SniperBuilder New(Node position)
                {
                    return new(position);
                }

                public override bool CanInitialStay(Board board)
                {
                    if (board.GetGrid(Position) is not Grid grid)
                    {
                        return false;
                    }
                    return grid.CanInitialStay();
                }

                public override Role Build()
                {
                    return Sniper.New(Position);
                }
            }
        }

        public class RoleComparerByType : IEqualityComparer<RoleBuilder>
        {
            public static readonly RoleComparerByType Instance = new();

            public bool Equals(RoleBuilder x, RoleBuilder y)
            {
                return x.GetType() == y.GetType();
            }

            public int GetHashCode(RoleBuilder obj)
            {
                return obj.GetType().GetHashCode();
            }
        }
    }

    public class Team
    {
        public readonly List<PlayerBuilder> Players;

        private Team(List<PlayerBuilder> players) => Players = players;

        public static Team? New(List<PlayerBuilder> players)
        {
            if (players.Count() < 2 || players.Count() > PlayerConsts.MaxPlayersPerTeam)
            {
                return null;
            }
            return new Team(players);
        }
    }
}
