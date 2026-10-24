using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Directories.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Protocol;
using MistForest.Core.Utils;
using Serilog;
using Serilog.Events;
using Tomlyn;
using ZLinq;
using Action = MistForest.Core.GameRefactored.Action;

namespace MistForest.Server
{
    public class ServerConfig
    {
        public int Port { get; set; } = 5137;
        public LogConfig Log { get; set; }

        public ServerConfig() => Log = new();

        public class LogConfig
        {
            public string Level { get; set; } = "Debug";
            public string Path { get; set; } = "./logs";
            public string Roll { get; set; } = "Day";
            public int RetainCount { get; set; } = 30;
        }
    }

    internal static class Program
    {
        private static readonly object Gate = new();
        private static int _nextSeat;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        private static ProjectDirectories _projectDir = ProjectDirectories.From(
            "com",
            "HitSZ-MistStudio",
            "MistForest"
        );
        private static readonly List<WebSocket> _sockets = new();
        private static ServerConfig _serverConfig = new();
        private static readonly Game Game = BuildScoutDuel();
        private static int _eventCursor = 0;

        private static async Task Main(string[] args)
        {
            Directory.CreateDirectory(_projectDir.ConfigDir);
            if (
                TomlSerializer.Deserialize<ServerConfig>(
                    File.ReadAllText(Path.Combine(_projectDir.ConfigDir, "server.toml")),
                    options: new TomlSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                )
                is ServerConfig config
            )
            {
                _serverConfig = config;
            }
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(
                    _serverConfig.Log.Level.ToLowerInvariant() switch
                    {
                        "verbose" => LogEventLevel.Verbose,
                        "debug" => LogEventLevel.Debug,
                        "information" or "info" => LogEventLevel.Information,
                        "warning" or "warn" => LogEventLevel.Warning,
                        "error" or "err" => LogEventLevel.Error,
                        "fatal" => LogEventLevel.Fatal,
                        _ => LogEventLevel.Debug,
                    }
                )
                .WriteTo.Console()
                .WriteTo.File(
                    _serverConfig.Log.Path,
                    rollingInterval: _serverConfig.Log.Roll.ToLowerInvariant() switch
                    {
                        "infinite" or "inf" => RollingInterval.Infinite,
                        "year" => RollingInterval.Year,
                        "month" => RollingInterval.Month,
                        "day" or "d" => RollingInterval.Day,
                        "hour" or "h" => RollingInterval.Hour,
                        "minute" or "min" => RollingInterval.Minute,
                        _ => RollingInterval.Day,
                    },
                    retainedFileCountLimit: _serverConfig.Log.RetainCount
                )
                .CreateLogger();
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            WebApplication app = builder.Build();
            app.UseWebSockets();
            CancellationToken stoppingToken = app.Lifetime.ApplicationStopping;
            app.Lifetime.ApplicationStopping.Register(AbortSockets);
            app.Map("/ws", context => HandleClientAsync(context, stoppingToken));
            Log.Information(
                $"MistForest referee listening on ws://0.0.0.0:{_serverConfig.Port}/ws"
            );
            app.Run($"http://0.0.0.0:{_serverConfig.Port}");
        }

        private static Game BuildScoutDuel()
        {
            Board board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            GameBuilder builder = new();
            if (builder.SetMode(Box<GameMode>.New(GameMode.Scout.Instance)) is null)
            {
                throw new InvalidOperationException("SetMode failed");
            }
            List<PlayerBuilder.RoleBuilder> Seat(int row, int col) =>
                new() { SniperBuilder.New([new SniperUnitBuilder()], board.At(row, col)!.Value)! };
            if (
                builder.SetPlayers(
                    Box.New(
                        new List<PlayerBuilder>
                        {
                            PlayerBuilder.New(Seat(1, 1))!,
                            PlayerBuilder.New(Seat(3, 3))!,
                        }
                    )
                )
                is null
            )
            {
                throw new InvalidOperationException("SetPlayers failed");
            }
            return builder.Build() ?? throw new InvalidOperationException("Build failed");
        }

        private static async Task HandleClientAsync(
            HttpContext context,
            CancellationToken stoppingToken
        )
        {
            if (context.WebSockets.IsWebSocketRequest is false)
            {
                context.Response.StatusCode = 400;
                return;
            }
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            int seat;
            lock (Gate)
            {
                if (_nextSeat < 2)
                {
                    seat = _nextSeat;
                    ++_nextSeat;
                }
                else
                {
                    seat = -1;
                }
                _sockets.Add(socket);
            }
            Log.Information($"client connected, seat={seat}");
            using CancellationTokenSource connectionToken =
                CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    stoppingToken
                );
            try
            {
                await SendAsync(socket, new WelcomeMsg { You = seat });
                StateMsg state;
                lock (Gate)
                {
                    state = BuildState();
                }
                await SendAsync(socket, state);

                byte[] buffer = new byte[4096];
                List<byte> pending = [];
                while (socket.State == WebSocketState.Open)
                {
                    ValueWebSocketReceiveResult result = await socket.ReceiveAsync(
                        new Memory<byte>(buffer),
                        connectionToken.Token
                    );
                    if (result.MessageType is WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(
                            socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            socket.CloseStatusDescription,
                            connectionToken.Token
                        );
                        break;
                    }
                    pending.AddRange(buffer.AsSpan(0, result.Count));
                    if (result.EndOfMessage is false)
                    {
                        continue;
                    }
                    string json = Encoding.UTF8.GetString(pending.ToArray());
                    pending.Clear();
                    await HandleMessageAsync(socket, seat, json);
                }
            }
            catch (OperationCanceledException) when (connectionToken.IsCancellationRequested) { }
            catch (WebSocketException e)
            {
                Log.Warning($"WebSocket disconnected: {e.Message}");
            }
            finally
            {
                if (socket.State is not WebSocketState.Closed)
                {
                    socket.Abort();
                }
                socket.Dispose();
                lock (Gate)
                {
                    _sockets.Remove(socket);
                }
                Log.Information($"client left, seat={seat}");
            }
        }

        private static void AbortSockets()
        {
            WebSocket[] sockets;
            lock (Gate)
            {
                sockets = [.. _sockets];
            }
            foreach (WebSocket socket in sockets)
            {
                try
                {
                    socket.Abort();
                }
                catch (ObjectDisposedException) { }
            }
        }

        private static Board.Node Decode(int pos)
        {
            return Game.Board.Decode(pos) ?? throw new ArgumentException($"pos {pos} out of range");
        }

        private static async Task HandleMessageAsync(WebSocket sender, int seat, string json)
        {
            try
            {
                EnvelopeMsg envelop = JsonSerializer.Deserialize<EnvelopeMsg>(json, JsonOptions)!;
                switch (envelop.Type)
                {
                    case "hello":
                        break;
                    case "move":
                        MoveMsg move = Deserialize<MoveMsg>(json);
                        await SubmitAsync(
                            sender,
                            seat,
                            move.Unit,
                            () => GeneralMove.New(move.Unit, Decode(move.Pos))
                        );
                        break;
                    case "scoutRow":
                        ScoutRowMsg row = Deserialize<ScoutRowMsg>(json);
                        await SubmitAsync(
                            sender,
                            seat,
                            row.Unit,
                            () => SniperScoutRow.New(row.Unit, row.Row)
                        );
                        break;
                    default:
                        await SendAsync(
                            sender,
                            new RejectMsg { Reason = $"unknown message type '{envelop.Type}'" }
                        );
                        break;
                }
            }
            catch (Exception e)
            {
                await SendAsync(sender, new RejectMsg { Reason = $"error: {e.Message}" });
            }
        }

        private static T Deserialize<T>(string json)
            where T : class
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new JsonException($"null {typeof(T).Name}");
        }

        private static async Task SubmitAsync(
            WebSocket sender,
            int seat,
            int unitId,
            Func<Action> factory
        )
        {
            string? reject = null;
            EventMsg[] fresh = [];
            StateMsg? state = null;
            lock (Gate)
            {
                if (seat < 0)
                {
                    reject = "spectators cannot act";
                }
                else if (PlayerIdOf(unitId) != seat)
                {
                    Log.Debug($"seat {seat} not your unit {unitId}");
                    reject = "not your unit";
                }
                else if (!Game.ExecuteAction(factory()))
                {
                    reject = "action rejected by referee";
                }
                else
                {
                    Event[] taken = new Event[Game.Events.Count - _eventCursor];
                    for (int i = _eventCursor; i < Game.Events.Count; ++i)
                    {
                        taken[i - _eventCursor] = Game.Events[i];
                    }
                    _eventCursor = Game.Events.Count;
                    fresh = [.. taken.Select(ToEventMsg)];
                    state = BuildState();
                }
            }
            if (reject is not null)
            {
                await SendAsync(sender, new RejectMsg { Reason = reject });
                return;
            }
            foreach (EventMsg message in fresh)
            {
                await BroadcastAsync(message);
            }
            await BroadcastAsync(state!);
        }

        private static async Task BroadcastAsync(object message)
        {
            WebSocket[] targets;
            lock (Gate)
            {
                targets = [.. _sockets];
            }
            foreach (WebSocket socket in targets)
            {
                await SendAsync(socket, message);
            }
        }

        private static EventMsg ToEventMsg(Event e)
        {
            switch (e)
            {
                case GeneralMoveEvent m:
                    return new EventMsg
                    {
                        Kind = "move",
                        Unit = m.UnitId,
                        Cells = EncodeAll(m.Path),
                        Success = true,
                    };
                case ScoutRowEvent m:
                    return new EventMsg
                    {
                        Kind = "scoutRow",
                        Unit = m.UnitId,
                        Target = m.Row,
                        Success = m.Success,
                    };
                default:
                    return new EventMsg { Kind = e.GetType().Name };
            }
        }

        private static int[] EncodeAll(Board.Node[] nodes)
        {
            return nodes.AsValueEnumerable().Select(Game.Board.Encode).ToArray();
        }

        private static int PlayerIdOf(int unitId)
        {
            return Game.PlayerIdFromUnitId(unitId);
        }

        private static async Task SendAsync(WebSocket socket, object message)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(message, message.GetType(), JsonOptions)
                );
                await socket.SendAsync(
                    new Memory<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None
                );
            }
            catch (Exception e)
            {
                Log.Error(e, $"send failed: {e.Message}");
            }
        }

        private static StateMsg BuildState()
        {
            StateMsg state = new()
            {
                CurrentPlayer = Game.CurrentPlayerId,
                Units =
                [
                    .. Game
                        .Players.SelectMany(player => player.Roles)
                        .SelectMany(role => role.GetUnits().AsEnumerable().ToArray())
                        .Select(unit => new UnitStateMsg
                        {
                            UnitId = unit.Id,
                            PlayerId = Game.PlayerIdFromUnitId(unit.Id),
                            Pos = Game.Board.Encode(unit.Position),
                            Health = unit.Health,
                            Alive = unit.Alive,
                        }),
                ],
            };
            return state;
        }
    }
}
