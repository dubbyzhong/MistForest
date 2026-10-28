using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using MistForest.Core.GameMap;
using MistForest.Core.Protocol;
using MistForest.Core.Utils;
using MistForest.Server.Manager;
using Serilog;

namespace MistForest.Server
{
    public class GameServer : BackgroundService
    {
        private static readonly ConnectionManager _connectionManager = new();
        private readonly RoomManager _roomManager = new();

        private readonly Channel<InboundMessage> _globalMsgChannel =
            Channel.CreateBounded<InboundMessage>(
                new BoundedChannelOptions(Program.ServerConfig.GlobalMsgCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false,
                }
            );

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (
                    InboundMessage message in _globalMsgChannel.Reader.ReadAllAsync(stoppingToken)
                )
                {
                    if (message is ClientPayload clientPayload)
                    {
                        if (clientPayload.IsLobbyMessage())
                        {
                            await HandleLobbyMessage(clientPayload, stoppingToken);
                        }
                    }
                    else if (message is ClientDisconnected clientDisconnected)
                    {
                        await HandleDisconnected(clientDisconnected, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                Log.Information("GameServer stopped");
            }
            finally
            {
                await _roomManager.StopAllAsync();
            }
        }

        private async Task HandleLobbyMessage(
            ClientPayload clientPayload,
            CancellationToken stoppingToken
        )
        {
            switch (clientPayload.Type())
            {
                case "createRoom":
                    await CreateRoom(clientPayload, stoppingToken);
                    break;
            }
        }

        private async Task CreateRoom(ClientPayload clientPayload, CancellationToken stoppingToken)
        {
            CreateRoomRequest createRoomRequest;
            try
            {
                createRoomRequest = JsonSerializer.Deserialize<CreateRoomRequest>(
                    clientPayload.Data.Span,
                    Program.JsonOptions
                );
            }
            catch (JsonException)
            {
                await clientPayload.Session.Close(stoppingToken);
                return;
            }

            if (
                Room.FromMsg(
                    ref createRoomRequest,
                    clientPayload.Session.SessionId,
                    Program.ServerConfig
                )
                is not Room room
            )
            {
                await clientPayload.Session.Close(stoppingToken);
                return;
            }
            clientPayload.Session.RoomId = room.Id;
            _roomManager.AddRoom(room);
            await clientPayload.Session.SendAsync(
                JsonSerializer.Serialize(CreateRoomResponse.New(room.Id)),
                stoppingToken
            );
            Log.Information(
                $"Create room with id={room.Id}, mode={createRoomRequest.Mode}, playerCount={createRoomRequest.PlayerCount}, teamCount={createRoomRequest.TeamCount}"
            );
        }

        private async Task HandleDisconnected(
            ClientDisconnected clientDisconnected,
            CancellationToken stoppingToken
        )
        {
            if (_connectionManager.GetById(clientDisconnected.SessionId)!.RoomId is string roomId)
            {
                Room room = _roomManager.GetById(roomId)!;
                await room.EnqueueAsync(clientDisconnected, stoppingToken);
            }
            _connectionManager.RemoveById(clientDisconnected.SessionId);
            Log.Information($"Client {clientDisconnected.SessionId} disconnected");
        }

        public async Task HandleClientAsync(
            HttpContext context,
            CancellationTokenSource connectionToken,
            CancellationToken stoppingToken
        )
        {
            string sessionId = Guid.NewGuid().ToString();
            PlayerSession session = _connectionManager.AddSession(
                sessionId,
                webSocket: Box.New(await context.WebSockets.AcceptWebSocketAsync())
            )!;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Program.ServerConfig.BufferSize);
            List<byte> pending = [];
            try
            {
                while (session.WebSocket.State is WebSocketState.Open)
                {
                    ValueWebSocketReceiveResult result = await session.WebSocket.ReceiveAsync(
                        new Memory<byte>(buffer),
                        connectionToken.Token
                    );
                    if (result.MessageType is WebSocketMessageType.Close)
                    {
                        CancellationTokenSource closeToken = new(
                            TimeSpan.FromSeconds(Program.ServerConfig.CloseTimeout)
                        );
                        await session.CloseOutputAsync(stoppingToken);
                        break;
                    }
                    if (result.MessageType is not WebSocketMessageType.Text)
                    {
                        CancellationTokenSource closeToken = new(
                            TimeSpan.FromSeconds(Program.ServerConfig.CloseTimeout)
                        );
                        await session.CloseOutputAsyncWithStatus(
                            WebSocketCloseStatus.InvalidMessageType,
                            "Invalid message type",
                            stoppingToken
                        );
                        break;
                    }
                    pending.AddRange(buffer.AsSpan(..result.Count));
                    if (result.EndOfMessage is false)
                    {
                        continue;
                    }
                    await _globalMsgChannel.Writer.WriteAsync(
                        ClientPayload.New(
                            _connectionManager.GetById(sessionId)!,
                            data: new Memory<byte>([.. pending])
                        ),
                        connectionToken.Token
                    );
                    pending.Clear();
                }
            }
            catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
            {
                Log.Information("Server closed; client disconnected");
                using CancellationTokenSource closeToken = new(
                    TimeSpan.FromSeconds(Program.ServerConfig.CloseTimeout)
                );
                try
                {
                    await session.CloseOutputAsyncWithStatus(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Server closed",
                        stoppingToken
                    );
                }
                catch { }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Client disconnected");
            }
            catch (WebSocketException e)
            {
                Log.Warning($"Websocket error: {e.Message}");
            }
            finally
            {
                if (session.WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    Log.Warning("Client aborted");
                    session.WebSocket.Abort();
                }
                if (stoppingToken.IsCancellationRequested is false)
                {
                    try
                    {
                        await _globalMsgChannel.Writer.WriteAsync(
                            ClientDisconnected.New(sessionId),
                            stoppingToken
                        );
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
                    { }
                    catch (ChannelClosedException) { }
                }
                ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
            }
        }
    }
}
