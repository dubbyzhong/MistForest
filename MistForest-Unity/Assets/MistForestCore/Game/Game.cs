using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Model;
using MistForest.Core.Utils;
using static MistForest.Core.GameRefactored.Game;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored
{
    public class Game
    {
        private readonly GameMode _mode;
        private readonly Board _board;
        private readonly List<Snapshot> _snapshots;
        private readonly List<Player> _players;

        public abstract class GameCount
        {
            private int _rolesPerPlayer;

            public class SingleCount : GameCount
            {
                private int _playerCount;

                public static SingleCount New(int rolesPerPlayer, int playerCount)
                {
                    return new() { _rolesPerPlayer = rolesPerPlayer, _playerCount = playerCount };
                }
            }

            public class TeamCount : GameCount
            {
                private int _teamCount;
                private int _playersPerTeam;

                public static TeamCount New(int rolesPerPlayer, int teamCount, int playersPerTeam)
                {
                    return new()
                    {
                        _rolesPerPlayer = rolesPerPlayer,
                        _teamCount = teamCount,
                        _playersPerTeam = playersPerTeam,
                    };
                }
            }
        }

        private readonly GameCount _gameCount;

        private Game(
            GameMode mode,
            Board board,
            List<Snapshot> snapshots,
            List<Player> players,
            GameCount gameCount
        )
        {
            _mode = mode;
            _board = board;
            _snapshots = snapshots;
            _players = players;
            _gameCount = gameCount;
        }

        public static Game New(
            GameMode mode,
            Board board,
            List<Player> players,
            GameCount gameCount
        )
        {
            return new(mode, board, new List<Snapshot>(), players, gameCount);
        }

        public void SaveSnapshot()
        {
            _snapshots.Add(Snapshot.New(_board, _players.ToArray()));
        }
    }

    public class GameBuilder
    {
        private GameMode? _mode;
        private Board? _board;
        private List<Player>? _players;
        private int? _rolesPerPlayer;
        private int? _playerCount;
        private int? _teamCount;
        private int? _playersPerTeam;
        private bool _isTeamGame;

        public GameBuilder? SetMode(Box<GameMode> mode)
        {
            if (_mode is not null || _board is not null)
            {
                return null;
            }
            if (mode.TryTake() is not GameMode modeInner)
            {
                return null;
            }
            _mode = modeInner;
            _board = modeInner switch
            {
                GameMode.Scout or GameMode.Heroes => Board.FromMode(modeInner, out _),
                _ => null,
            };
            return this;
        }

        public GameBuilder? SetBoard(Box<Board> board)
        {
            if (_mode is null || _board is not null)
            {
                return null;
            }
            if (board.TryTake() is not Board boardInner)
            {
                return null;
            }
            _board = boardInner;
            return this;
        }

        public GameBuilder? SetPlayers(Box<List<PlayerBuilder>> players)
        {
            if (_players is not null || _board is null || _mode is null)
            {
                return null;
            }
            if (players.TryTake() is not List<PlayerBuilder> playersInner)
            {
                return null;
            }
            if (
                playersInner.Count == 0
                || playersInner.Count > (_mode is GameMode.Scout ? 1 : PlayerConsts.MaxPlayerCount)
            )
            {
                return null;
            }
            int rolesPerPlayer = playersInner[0].Roles.Count();
            if (
                playersInner.Any(player =>
                    player.TeamId is not null
                    || player.Roles.Count() != rolesPerPlayer
                    || _mode is GameMode.Scout && player.Roles[0] is not RoleBuilder.SniperBuilder
                    || player.Roles.Any(role => !role.CanInitialStay(_board!))
                )
            )
            {
                return null;
            }
            _rolesPerPlayer = rolesPerPlayer;
            _playerCount = playersInner.Count;
            _players = playersInner.Select(player => player.Build()).ToList();
            _isTeamGame = false;
            return this;
        }

        public GameBuilder? SetTeams(Box<List<Team>> teams)
        {
            if (_players is not null || _board is null || _mode is null or GameMode.Scout)
            {
                return null;
            }
            if (teams.TryTake() is not List<Team> teamsInner)
            {
                return null;
            }
            if (teamsInner.Count < 2 || teamsInner.Count > PlayerConsts.MaxTeamCount)
            {
                return null;
            }
            int playersPerTeam = teamsInner[0].Players.Count();
            if (
                playersPerTeam < 2
                || playersPerTeam > PlayerConsts.MaxPlayersPerTeam
                || teamsInner.Any(team => team.Players.Count() != playersPerTeam)
            )
            {
                return null;
            }
            int rolesPerPlayer = teamsInner[0].Players[0].Roles.Count();
            var playersInner = teamsInner.SelectMany(team => team.Players);
            if (
                playersInner.Any(player =>
                    player.TeamId is not null
                    || player.Roles.Count() != rolesPerPlayer
                    || player.Roles.Any(role => !role.CanInitialStay(_board!))
                )
            )
            {
                return null;
            }
            foreach (var (team, teamId) in teamsInner.Select((team, index) => (team, index)))
            {
                team.Players.ForEach(player => player.TeamId = teamId);
            }
            _rolesPerPlayer = rolesPerPlayer;
            _teamCount = teamsInner.Count;
            _playersPerTeam = playersPerTeam;
            _players = playersInner.Select(player => player.Build()).ToList();
            _isTeamGame = true;
            return this;
        }

        public Game? Build()
        {
            if (_mode is null || _board is null || _players is null)
            {
                return null;
            }
            GameCount gameCount = _isTeamGame switch
            {
                true => GameCount.TeamCount.New(
                    _rolesPerPlayer!.Value,
                    _teamCount!.Value,
                    _playersPerTeam!.Value
                ),
                false => GameCount.SingleCount.New(_rolesPerPlayer!.Value, _playerCount!.Value),
            };
            return New(mode: _mode, board: _board, players: _players, gameCount: gameCount);
        }
    }
}
