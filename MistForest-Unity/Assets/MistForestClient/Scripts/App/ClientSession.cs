using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MistForest.Client.Message;
using MistForest.Client.Utils;
using MistForest.Core.Protocol;
using NativeWebSocket;
using Newtonsoft.Json;

namespace MistForest.Client.App
{
    public sealed class ClientSession
    {
        // private readonly WebSocketTransport _transport;
        private readonly Queue<ClientMessage> _messages;
        private WebSocket? _webSocket;
        private ConnectionState _connectionState;
        public string RoomId { get; private set; }

        public event Action<string>? Error;
        public event Action<int, string>? Disconnected;
        public event Action<CreateRoomResponse>? RoomCreated;
        public event Action<RejectMsg>? RequestRejected;

        public bool IsConnected => _connectionState is ConnectionState.Connected;

        private ClientSession(string roomId, Queue<ClientMessage> messages)
        {
            RoomId = roomId;
            _messages = messages;
        }

        public static ClientSession New()
        {
            return new(roomId: "", messages: new())
            {
                _connectionState = ConnectionState.Disconnected,
            };
        }

        public async Task ConnectAsync(string url)
        {
            if (_connectionState is not ConnectionState.Disconnected)
            {
                return;
            }
            _connectionState = ConnectionState.Connecting;

            TaskCompletionSource<bool> opened = new();
            try
            {
                _webSocket = new(url);

                _webSocket.OnOpen += () =>
                {
                    _connectionState = ConnectionState.Connected;
                    opened.TrySetResult(true);
                };

                _webSocket.OnMessage += bytes => _messages.Enqueue(Payload.New(bytes));

                _webSocket.OnError += error =>
                {
                    if (_connectionState is ConnectionState.Connecting)
                    {
                        opened.TrySetException(new InvalidOperationException(error));
                    }
                    else
                    {
                        _messages.Enqueue(
                            ClientError.New((int)WebSocketCloseCode.InvalidData, error)
                        );
                    }
                };

                _webSocket.OnClose += code =>
                {
                    bool wasConnecting = _connectionState is ConnectionState.Connecting;
                    _messages.Enqueue(ClientDisconnected.New((int)code, code.ToString()));
                    if (wasConnecting)
                    {
                        opened.TrySetException(
                            new InvalidOperationException(
                                $"WebSocket closed before opening: {code}"
                            )
                        );
                    }
                };

                _ = ObserveConnectAsync(opened);
                await opened.Task;
            }
            catch (Exception e)
            {
                _connectionState = ConnectionState.Disconnected;
                ReportError(e.Message);
            }
        }

        private async Task ObserveConnectAsync(TaskCompletionSource<bool> opened)
        {
            try
            {
                await _webSocket!.Connect();
            }
            catch (Exception e)
            {
                opened.TrySetException(e);
            }
        }

        public void Pump()
        {
            while (_messages.Count > 0)
            {
                ClientMessage message = _messages.Dequeue();
                switch (message)
                {
                    case Payload payload:
                        Dispatch(payload);
                        break;
                    case ClientDisconnected disconnected:
                        RoomId = "";
                        _ = DisconnectAsync(
                            disconnected.CloseCode,
                            disconnected.CloseStatusDescription
                        );
                        break;
                    case ClientError error:
                        _ = DisconnectAsync(error.CloseCode, error.Error);
                        ReportError(error.Error);
                        break;
                }
            }
        }

        private void Dispatch(Payload payload)
        {
            switch (payload.Type())
            {
                case "createRoom":
                    CreateRoomResponse createRoomResponse =
                        JsonConvert.DeserializeObject<CreateRoomResponse>(
                            Encoding.UTF8.GetString(payload.Data.Span)
                        )!;
                    RoomId = createRoomResponse.RoomId;
                    RoomCreated?.Invoke(createRoomResponse);
                    break;
                case "reject":
                    RejectMsg reject = JsonConvert.DeserializeObject<RejectMsg>(
                        Encoding.UTF8.GetString(payload.Data.Span)
                    )!;
                    RequestRejected?.Invoke(reject);
                    break;
            }
        }

        private void ReportError(string message)
        {
            Error?.Invoke(message);
        }

        public async Task DisconnectAsync(int closeCode, string description)
        {
            if (_connectionState is not ConnectionState.Connected)
            {
                return;
            }
            _connectionState = ConnectionState.Disconnecting;
            try
            {
                if (_webSocket?.State is WebSocketState.Open)
                {
                    int validCode =
                        closeCode == (int)WebSocketCloseCode.Normal
                        || (closeCode >= 3000 && closeCode <= 4999)
                            ? closeCode
                            : (int)WebSocketCloseCode.Normal;
                    await _webSocket.Close((WebSocketCloseCode)validCode, description);
                }
            }
            catch (Exception e)
            {
                _connectionState = ConnectionState.Disconnected;
                ReportError(e.Message);
            }
            finally
            {
                Disconnected?.Invoke(closeCode, description);
                _connectionState = ConnectionState.Disconnected;
                RoomId = "";
            }
        }

        public async Task CreateRoomRequestAsync(string mode, int playerCount, int teamCount)
        {
            if (
                _connectionState is not ConnectionState.Connected
                || _webSocket is not WebSocket webSocket
            )
            {
                ReportError("Not connected server yet");
                return;
            }

            await webSocket.SendText(
                JsonConvert.SerializeObject(
                    CreateRoomRequest.New(mode, playerCount, teamCount),
                    JsonSettings.Default
                )
            );
        }

        private enum ConnectionState
        {
            Disconnecting,
            Disconnected,
            Connecting,
            Connected,
        }
    }
}
