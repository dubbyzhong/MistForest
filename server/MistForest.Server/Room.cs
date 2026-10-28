using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using MistForest.Core.GameMap;
using MistForest.Core.Protocol;
using Serilog;

namespace MistForest.Server
{
    public class Room
    {
        public string Id { get; set; }
        public List<string> SessionIds { get; set; }
        public GameMode Mode { get; set; }
        public int PlayerCount { get; set; }
        public int TeamCount { get; set; }
        public RoomStatus Status { get; set; }
        private readonly ServerConfig _serverConfig;
        private readonly Channel<InboundMessage> _msgChannel;
        private Task? _loopTask;
        private readonly CancellationTokenSource _forceStop;
        public Task Completion => _loopTask ?? Task.CompletedTask;

        private Room(
            string id,
            List<string> sessionIds,
            GameMode mode,
            ServerConfig serverConfig,
            Channel<InboundMessage> msgChannel,
            CancellationTokenSource forceStop
        )
        {
            Id = id;
            SessionIds = sessionIds;
            Mode = mode;
            _serverConfig = serverConfig;
            _msgChannel = msgChannel;
            _forceStop = forceStop;
        }

        public static Room New(
            string id,
            List<string> sessions,
            GameMode mode,
            int playerCount,
            int teamCount,
            RoomStatus status,
            ServerConfig serverConfig
        )
        {
            return new(
                id,
                sessions,
                mode,
                serverConfig,
                msgChannel: Channel.CreateBounded<InboundMessage>(
                    new BoundedChannelOptions(serverConfig.GlobalMsgCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = true,
                        SingleWriter = false,
                    }
                ),
                forceStop: new()
            )
            {
                PlayerCount = playerCount,
                TeamCount = teamCount,
                Status = status,
            };
        }

        public static Room? FromMsg(
            ref CreateRoomRequest createRoomRequest,
            string sessionId,
            ServerConfig serverConfig
        )
        {
            static GameMode? FromModeStr(string modeStr)
            {
                Match match = SelfDefinedModeRegex().Match(modeStr);
                if (!match.Success)
                {
                    return null;
                }
                if (
                    int.TryParse(match.Groups[1].Value, out int height)
                    && int.TryParse(match.Groups[2].Value, out int width)
                )
                {
                    return GameMode.SelfDefine.New(height, width);
                }
                return null;
            }
            GameMode? mode = createRoomRequest.Mode switch
            {
                "scout" => GameMode.Scout.Instance,
                "heroes" => GameMode.Heroes.Instance,
                string modeStr => FromModeStr(modeStr),
            };
            if (mode is null)
            {
                return null;
            }
            if (
                createRoomRequest.PlayerCount < 2
                || createRoomRequest.TeamCount != 0
                    && (
                        createRoomRequest.TeamCount < 2
                        || createRoomRequest.PlayerCount % createRoomRequest.TeamCount != 0
                    )
            )
            {
                return null;
            }
            string roomId = Guid.NewGuid().ToString("N");
            return New(
                roomId,
                sessions: [sessionId],
                mode,
                createRoomRequest.PlayerCount,
                createRoomRequest.TeamCount,
                RoomStatus.Prepare,
                serverConfig
            );

            [GeneratedRegex("^([0-9]+),([0-9]+)$")]
            static extern Regex SelfDefinedModeRegex();
        }

        internal async Task EnqueueAsync(
            InboundMessage inboundMessage,
            CancellationToken stoppingToken
        )
        {
            await _msgChannel.Writer.WriteAsync(inboundMessage, stoppingToken);
        }

        public void Start()
        {
            if (_loopTask != null)
            {
                throw new InvalidOperationException("Room already started");
            }
            _loopTask = RunAsync(_forceStop.Token);
        }

        private async Task RunAsync(CancellationToken forceStop)
        {
            try
            {
                await foreach (InboundMessage message in _msgChannel.Reader.ReadAllAsync(forceStop))
                {
                    switch (message)
                    {
                        case ClientPayload payload:
                            break;
                        case ClientDisconnected disconnected:
                            HandleClientDisconnected(disconnected);
                            break;
                    }
                }
            }
            catch (OperationCanceledException e) when (e.CancellationToken == forceStop) { }
        }

        private void HandleClientDisconnected(ClientDisconnected disconnected)
        {
            SessionIds.Remove(disconnected.SessionId);
            if (SessionIds.Count == 0)
            {
                Complete();
                Log.Information($"Room {Id} completed");
            }
        }

        public void Complete()
        {
            _msgChannel.Writer.TryComplete();
        }
    }

    public enum RoomStatus
    {
        Prepare,
        Playing,
    }
}
