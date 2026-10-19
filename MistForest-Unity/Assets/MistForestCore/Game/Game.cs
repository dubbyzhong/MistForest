using System;
using System.Collections.Generic;
using System.Linq;
using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using static MistForest.Core.GameRefactored.Game;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.GameRefactored
{
    public class Game
    {
        private readonly GameMode _mode;
        public Board Board { get; private set; }
        private readonly List<Snapshot> _snapshots;
        private readonly Role[] _roles;
        private readonly Unit[] _units;
        private readonly Player[] _players;
        public List<Event> Events;
        private readonly GameCount _gameCount;
        public int CurrentPlayerId { get; private set; }
        private Bitset _roundActed;
        private int _turnCount;
        private int _roundCount;
        private GameState _gameState;
        private bool _isGameOver => _gameState is GameState.Win or GameState.Draw;

        public abstract class GameCount
        {
            private int _rolesPerPlayer;
            public int PlayerCount { get; private set; }

            public class SingleGameCount : GameCount
            {
                public static SingleGameCount New(int rolesPerPlayer, int playerCount)
                {
                    return new() { _rolesPerPlayer = rolesPerPlayer, PlayerCount = playerCount };
                }
            }

            public class TeamGameCount : GameCount
            {
                public int TeamCount { get; private set; }
                public int PlayersPerTeam { get; private set; }
                public new int PlayerCount => TeamCount * PlayersPerTeam;

                public static TeamGameCount New(
                    int rolesPerPlayer,
                    int teamCount,
                    int playersPerTeam
                )
                {
                    return new()
                    {
                        _rolesPerPlayer = rolesPerPlayer,
                        TeamCount = teamCount,
                        PlayersPerTeam = playersPerTeam,
                    };
                }
            }
        }

        private Game(
            GameMode mode,
            Board board,
            List<Snapshot> snapshots,
            Role[] roles,
            Unit[] units,
            Player[] players,
            GameCount gameCount,
            List<Event> events,
            Bitset roundActed,
            GameState gameState
        )
        {
            _mode = mode;
            Board = board;
            _snapshots = snapshots;
            _roles = roles;
            _units = units;
            _players = players;
            _gameCount = gameCount;
            Events = events;
            _roundActed = roundActed;
            _gameState = gameState;
        }

        public static Game New(
            GameMode mode,
            Board board,
            Role[] roles,
            Unit[] units,
            Player[] players,
            GameCount gameCount,
            int firstPlayerId
        )
        {
            return new(
                mode,
                board,
                snapshots: new List<Snapshot>(),
                roles,
                units,
                players,
                gameCount,
                events: new List<Event>(),
                roundActed: Bitset.WithCapacity(gameCount.PlayerCount),
                gameState: GameState.Playing.Instance
            )
            {
                CurrentPlayerId = firstPlayerId,
            };
        }

        public void SaveSnapshot()
        {
            _snapshots.Add(Snapshot.New(Board, _players.ToArray()));
        }

        public int NextPlayerId(int playerId)
        {
            if (_gameCount is GameCount.SingleGameCount singleCountInner)
            {
                return (playerId + 1) % singleCountInner.PlayerCount;
            }
            else if (_gameCount is GameCount.TeamGameCount teamCountInner)
            {
                if (
                    playerId + teamCountInner.PlayersPerTeam
                    >= teamCountInner.TeamCount * teamCountInner.PlayersPerTeam
                )
                {
                    return (playerId + 1 + teamCountInner.PlayersPerTeam)
                        % (teamCountInner.TeamCount * teamCountInner.PlayersPerTeam)
                        % teamCountInner.PlayersPerTeam;
                }
                else
                {
                    return playerId + teamCountInner.PlayersPerTeam;
                }
            }
            throw new NotImplementedException();
        }

        public bool ExecuteAction(Action action)
        {
            if (action is ISniperAction and Action sniperAction)
            {
                if (
                    _players[CurrentPlayerId].Roles.FirstOrDefault(role => role is Sniper)
                        is Sniper sniper
                    && sniper.JudgeAction(sniperAction, this)
                )
                {
                    sniper.ExecuteAction(sniperAction, this);
                    if (sniper.HasActionLeft() is false)
                    {
                        EndTurn();
                    }
                }
            }
            return false;
        }

        private void EndTurn()
        {
            ++_turnCount;
            _roundActed[CurrentPlayerId] = true;
            _gameState = CheckVictory();
            if (_isGameOver)
            {
                return;
            }
            bool nextRound = _players
                .Where(player => player.Alive)
                .All(player => _roundActed[player.Id]);
            if (nextRound)
            {
                ++_roundCount;
                foreach (Unit unit in _units.Where(unit => unit.Alive))
                {
                    unit.EndRound(this);
                }
            }
            do
            {
                CurrentPlayerId = NextPlayerId(CurrentPlayerId);
            } while (!_players[CurrentPlayerId].Alive);
        }

        public abstract record GameState
        {
            public record Playing : GameState
            {
                public static readonly Playing Instance = new();

                public static Playing New() => new();
            }

            public record Win : GameState
            {
                public int WinnerId { get; private set; }

                public static Win New(int winnerId)
                {
                    return new() { WinnerId = winnerId };
                }
            }

            public record Draw : GameState
            {
                public static readonly Draw Instance = new();

                public static Draw New() => new();
            }
        }

        private GameState CheckVictory()
        {
            if (_roundCount >= GameConsts.MaxRoundCount)
            {
                return GameState.Draw.New();
            }
            if (_gameCount is GameCount.SingleGameCount singleCountInner)
            {
                int? firstAlive = _players.FirstOrDefault(player => player.Alive)?.Id;
                if (firstAlive is null)
                {
                    return GameState.Draw.Instance;
                }
                if (_players[firstAlive!.Value..].Any(player => player.Alive))
                {
                    return GameState.Playing.Instance;
                }
                else
                {
                    return GameState.Win.New(firstAlive.Value);
                }
            }
            else if (_gameCount is GameCount.TeamGameCount teamCountInner)
            {
                int? firstAlive = Enumerable
                    .Range(0, teamCountInner.TeamCount)
                    .FirstOrDefault(teamId => GetTeamPlayers(teamId).Any(player => player.Alive));
                if (firstAlive is null)
                {
                    return GameState.Draw.Instance;
                }
                if (
                    Enumerable
                        .Range(firstAlive!.Value + 1, teamCountInner.TeamCount)
                        .Any(teamId => GetTeamPlayers(teamId).Any(player => player.Alive))
                )
                {
                    return GameState.Playing.Instance;
                }
                else
                {
                    return GameState.Win.New(firstAlive.Value);
                }
            }
            throw new NotImplementedException();
        }

        private Player[] GetTeamPlayers(int teamId)
        {
            return _players[
                (teamId * PlayerConsts.MaxPlayersPerTeam)..(
                    (teamId + 1) * PlayerConsts.MaxPlayersPerTeam
                )
            ];
        }

        public Unit? GetUnit(int unitId)
        {
            if (unitId >= _units.Length || unitId < 0)
            {
                return null;
            }
            return _units[unitId];
        }
    }

    public class GameBuilder
    {
        private GameMode? _mode;
        private Board? _board;
        private Player[]? _players;
        private Role[]? _roles;
        private Unit[]? _units;
        private int? _rolesPerPlayer;
        private int? _playerCount;
        private int? _teamCount;
        private int? _playersPerTeam;
        private bool _isTeamGame;
        private int? _firstPlayerId;

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
            if (playersInner.Count < 2 || playersInner.Count > PlayerConsts.MaxPlayerCount)
            {
                return null;
            }
            int rolesPerPlayer = playersInner[0].Roles.Count();
            if (_mode is GameMode.Scout && rolesPerPlayer != 1)
            {
                return null;
            }
            if (
                playersInner.Any(player =>
                    player.TeamId is not null
                    || player.Id is not null
                    || player.Roles.Count() != rolesPerPlayer
                    || _mode is GameMode.Scout && player.Roles[0] is not SniperBuilder
                    || player.Roles.Any(role => !role.CanInitialStay(_board!))
                )
            )
            {
                return null;
            }

            var playerBuilders = playersInner
                .Select((player, index) => player.SetId(index)!)
                .ToList();
            var roleBuilders = playerBuilders
                .SelectMany(player =>
                    player.Roles.Select(role => role.SetPlayerId(player.Id!.Value)!)
                )
                .Select((role, index) => role.SetId(index)!)
                .ToList();
            var unitBuilders = roleBuilders
                .SelectMany(role =>
                    role.UnitBuilder.Select(unit => unit.SetRoleId(role.Id!.Value)!)
                )
                .Select((unit, index) => unit.SetId(index)!)
                .Select(unit =>
                    _mode is GameMode.Scout && unit is SniperUnitBuilder sniperUnit
                        ? sniperUnit.SetAttackRange(SniperConsts.SniperAttackRangeScout)
                        : ((SniperUnitBuilder)unit).SetAttackRange(SniperConsts.SniperAttackRange)
                )
                .ToList();
            _rolesPerPlayer = rolesPerPlayer;
            _playerCount = playersInner.Count;
            _roles = roleBuilders.Select(role => role.Build()!).ToArray();
            _units = unitBuilders.Select(unit => unit.Build()!).ToArray();
            foreach (Unit unit in _units)
            {
                _board!.GetGrid(unit.Position)!.Units.Add(unit.Id);
            }
            _players = playerBuilders.Select(player => player.Build()!).ToArray();
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
            var playersInner = teamsInner.SelectMany(team => team.Players).ToList();
            if (
                playersInner.Any(player =>
                    player.TeamId is not null
                    || player.Id is not null
                    || player.Roles.Count() != rolesPerPlayer
                    || player.Roles.Any(role => !role.CanInitialStay(_board!))
                )
            )
            {
                return null;
            }
            foreach (var (team, teamId) in teamsInner.Select((team, index) => (team, index)))
            {
                team.Players.ForEach(player => player.SetTeamId(teamId));
            }
            var playerBuilders = playersInner
                .Select((player, index) => player.SetId(index)!)
                .ToList();
            var roleBuilders = playerBuilders
                .SelectMany(player =>
                    player.Roles.Select(role => role.SetPlayerId(player.Id!.Value)!)
                )
                .Select((role, index) => role.SetId(index)!)
                .ToList();
            var unitBuilders = roleBuilders
                .SelectMany(role =>
                    role.UnitBuilder.Select(unit => unit.SetRoleId(role.Id!.Value)!)
                )
                .Select((unit, index) => unit.SetId(index)!)
                .ToList();
            _rolesPerPlayer = rolesPerPlayer;
            _teamCount = teamsInner.Count;
            _playersPerTeam = playersPerTeam;
            _roles = roleBuilders.Select(role => role.Build()!).ToArray();
            _units = unitBuilders.Select(unit => unit.Build()!).ToArray();
            foreach (Unit unit in _units)
            {
                _board!.GetGrid(unit.Position)!.Units.Add(unit.Id);
            }
            _players = playersInner.Select(player => player.Build()!).ToArray();
            _isTeamGame = true;
            return this;
        }

        public GameBuilder? SetFirstPlayerId(int firstPlayerId)
        {
            if (_players is null)
            {
                return null;
            }
            int playerCount = _isTeamGame
                ? _playersPerTeam!.Value * _teamCount!.Value
                : _playerCount!.Value;
            if (firstPlayerId < 0 || firstPlayerId >= playerCount)
            {
                return null;
            }
            _firstPlayerId = firstPlayerId;
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
                true => GameCount.TeamGameCount.New(
                    _rolesPerPlayer!.Value,
                    _teamCount!.Value,
                    _playersPerTeam!.Value
                ),
                false => GameCount.SingleGameCount.New(_rolesPerPlayer!.Value, _playerCount!.Value),
            };
            return New(
                mode: _mode,
                board: _board,
                roles: _roles!,
                units: _units!,
                players: _players,
                gameCount: gameCount,
                firstPlayerId: _firstPlayerId switch
                {
                    null => 0,
                    int firstPlayerIdInner => firstPlayerIdInner,
                }
            );
        }
    }
}
