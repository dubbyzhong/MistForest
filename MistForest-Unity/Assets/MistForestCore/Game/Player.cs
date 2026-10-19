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
        public int Id { get; internal set; }
        public int? TeamId { get; internal set; }
        public bool Alive => Roles.Any(role => role.Alive);

        public readonly Role[] Roles;

        private Player(Role[] Roles) => this.Roles = Roles;

        public static Player New(int id, Role[] roles)
        {
            return new Player(roles) { Id = id };
        }

        public Player Clone()
        {
            return new(Roles.Select(role => role.Clone()).ToArray()) { Id = Id, TeamId = TeamId };
        }
    }

    public class PlayerBuilder
    {
        public int? Id { get; internal set; }
        public int? TeamId { get; internal set; }
        public List<RoleBuilder> Roles { get; private set; }

        private PlayerBuilder(List<RoleBuilder> roles) => Roles = roles;

        public PlayerBuilder? SetId(int id)
        {
            if (Id is not null)
            {
                return null;
            }
            Id = id;
            return this;
        }

        public PlayerBuilder? SetTeamId(int teamId)
        {
            if (TeamId is not null)
            {
                return null;
            }
            TeamId = teamId;
            return this;
        }

        public Player? Build()
        {
            if (Id is not int id)
            {
                return null;
            }
            return Player.New(id, roles: Roles.Select(role => role.Build()!).ToArray());
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
            public int? Id { get; internal set; }
            public int? PlayerId { get; internal set; }
            public UnitBuilder[] UnitBuilder { get; private set; }
            public Role? Built;

            protected RoleBuilder(UnitBuilder[] unitBuilder) => UnitBuilder = unitBuilder;

            public abstract bool CanInitialStay(Board board);

            public RoleBuilder? SetId(int id)
            {
                if (Id is not null)
                {
                    return null;
                }
                Id = id;
                return this;
            }

            public RoleBuilder? SetPlayerId(int id)
            {
                if (PlayerId is not null)
                {
                    return null;
                }
                PlayerId = id;
                return this;
            }

            public abstract Role? Build();
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
