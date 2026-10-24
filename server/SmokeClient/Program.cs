using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Directories.Net;
using MistForest.Core.Protocol;
using Serilog;
using Tomlyn;
using Tomlyn.Model;

namespace SmokeClient
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        private static readonly ProjectDirectories _projectDirectories = ProjectDirectories.From(
            "com",
            "HitSZ-MistStudio",
            "MistForest"
        );

        private static async Task Main(string[] args)
        {
            using ClientWebSocket socket = new();
            var table = TomlSerializer.Deserialize<TomlTable>(
                File.ReadAllText(Path.Combine(_projectDirectories.ConfigDir, "server.toml"))
            )!;
            int port = Convert.ToInt32(table["port"]);
            await socket.ConnectAsync(new Uri($"ws://localhost:{port}/ws"), CancellationToken.None);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
            Log.Information("connected");

            int seat = -1;
            bool acted = false;
            byte[] buffer = new byte[4096];
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && socket.State == WebSocketState.Open)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(
                    new Memory<byte>(buffer),
                    CancellationToken.None
                );
                if (result.MessageType is WebSocketMessageType.Close)
                {
                    break;
                }
                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                EnvelopeMsg envelope = JsonSerializer.Deserialize<EnvelopeMsg>(json, _json)!;
                switch (envelope.Type)
                {
                    case "welcome":
                        seat = JsonSerializer.Deserialize<WelcomeMsg>(json, _json)!.You;
                        Log.Information($"seat = {seat}");
                        break;
                    case "state":
                        StateMsg state = JsonSerializer.Deserialize<StateMsg>(json, _json)!;
                        Log.Information(
                            $"state: currentPlayer=P{state.CurrentPlayer}, units=[{string.Join(", ", state.Units.Select(u => $"#{u.UnitId}(P{u.PlayerId})@{u.Pos} hp{u.Health}"))}]"
                        );
                        if (seat >= 0 && state.CurrentPlayer == seat && !acted)
                        {
                            acted = true;
                            UnitStateMsg mine = state.Units.First(u => u.PlayerId == seat);
                            if (seat == 0)
                            {
                                int to = mine.Pos % 3 < 2 ? mine.Pos + 1 : mine.Pos - 1;
                                Log.Information($"to: {to}");
                                await SendAsync(
                                    socket,
                                    new MoveMsg { Unit = mine.UnitId, Pos = to }
                                );
                            }
                            else
                            {
                                await SendAsync(
                                    socket,
                                    new ScoutRowMsg { Unit = mine.UnitId, Row = 1 }
                                );
                            }
                        }
                        break;
                    case "event":
                        EventMsg e = JsonSerializer.Deserialize<EventMsg>(json, _json)!;
                        Log.Information(
                            $"event: {e.Kind} unit={e.Unit} target={e.Target} cells=[{string.Join(",", e.Cells)}] ok={e.Success}"
                        );
                        break;
                    case "reject":
                        Log.Error(
                            $"reject: {JsonSerializer.Deserialize<RejectMsg>(json, _json)!.Reason}"
                        );
                        break;
                }
            }
            Log.Information("done");
        }

        private static async Task SendAsync<T>(ClientWebSocket socket, T message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _json));
            await socket.SendAsync(
                new Memory<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
