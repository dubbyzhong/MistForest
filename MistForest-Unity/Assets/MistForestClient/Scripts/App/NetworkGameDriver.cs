using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MistForest.Client.Utils;
using MistForest.Core.GameMap;
using MistForest.Core.Protocol;
using Newtonsoft.Json;
using UnityEngine;

namespace MistForest.Client.App
{
    public class NetworkGameDriver
    {
        private readonly ClientWebSocket _socket = new();
        private readonly Queue<string> _inbox = new();
        private readonly object _inboxGate = new();

        public Board Board { get; }
        public StateMsg State { get; private set; }
        public int YouSeat { get; private set; } = -1;
        public bool Connected { get; private set; }
        public string LastError { get; set; } = "";

        public readonly Queue<EventMsg> NewEvents = new();

        public NetworkGameDriver()
        {
            Board = Board.FromMode(GameMode.Scout.Instance, out _)!;
            State = null!;
        }

        public async Task ConnectAsync(string url)
        {
            try
            {
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                await _socket.ConnectAsync(new Uri(url), timeout.Token);
                _ = ReceiveLoopAsync();
                await SendAsync(new HelloMsg());
                Connected = true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (_socket.State is WebSocketState.Open)
                {
                    StringBuilder text = new();
                    ValueWebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(
                            new Memory<byte>(buffer),
                            CancellationToken.None
                        );
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            // Debug.Log("disconnected");
                            return;
                        }
                        text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (result.EndOfMessage is false);
                    lock (_inboxGate)
                    {
                        _inbox.Enqueue(text.ToString());
                    }
                }
            }
            catch { }
        }

        private async Task SendAsync<T>(T message)
        {
            if (_socket.State is not WebSocketState.Open)
            {
                return;
            }
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(message, JsonSettings.Default)
                );
                await _socket.SendAsync(
                    new Memory<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None
                );
            }
            catch { }
        }

        public Task SendMoveAsync(int unitId, Board.Node target)
        {
            return SendAsync(new MoveMsg { Unit = unitId, Pos = Board.Encode(target) });
        }

        public void Pump()
        {
            while (true)
            {
                string json;
                lock (_inboxGate)
                {
                    if (_inbox.Count == 0)
                    {
                        break;
                    }
                    json = _inbox.Dequeue();
                }
                Dispatch(json);
            }
            Connected = _socket.State is WebSocketState.Open;
        }

        private void Dispatch(string json)
        {
            EnvelopeMsg envelope = JsonConvert.DeserializeObject<EnvelopeMsg>(json)!;
            switch (envelope.Type)
            {
                case "welcome":
                    YouSeat = JsonConvert.DeserializeObject<WelcomeMsg>(json)!.You;
                    break;
                case "state":
                    State = JsonConvert.DeserializeObject<StateMsg>(json)!;
                    break;
                case "event":
                    NewEvents.Enqueue(JsonConvert.DeserializeObject<EventMsg>(json)!);
                    break;
                case "reject":
                    LastError = JsonConvert.DeserializeObject<RejectMsg>(json)!.Reason;
                    break;
            }
        }

        public void Disconnected()
        {
            try
            {
                if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                    _socket
                        .CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Play Mode stopped",
                            timeout.Token
                        )
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            finally
            {
                _socket.Dispose();
                Connected = false;
            }
        }
    }
}
