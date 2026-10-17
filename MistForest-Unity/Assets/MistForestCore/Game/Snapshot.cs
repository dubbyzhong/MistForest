using System.Linq;
using MistForest.Core.GameMap;

namespace MistForest.Core.GameRefactored
{
    public record Snapshot
    {
        private readonly Board _board;
        private readonly Player[] _players;

        private Snapshot(Board board, Player[] players)
        {
            _board = board;
            _players = players;
        }

        public static Snapshot New(Board board, Player[] players)
        {
            return new(
                board: board.Clone(),
                players: players.Select(player => player.Clone()).ToArray()
            );
        }
    }
}
