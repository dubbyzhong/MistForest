using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MistForest.Server
{
    public class PlayerSession
    {
        public string SessionId { get; set; }
        public WebSocket WebSocket { get; set; }
        private readonly SemaphoreSlim _sendLock = new(initialCount: 1, maxCount: 1);
        public string? RoomId;

        private PlayerSession(string sessionId, WebSocket webSocket)
        {
            SessionId = sessionId;
            WebSocket = webSocket;
        }

        public static PlayerSession New(string sessionId, WebSocket webSocket)
        {
            return new PlayerSession(sessionId, webSocket) { RoomId = null };
        }

        public async Task Close(CancellationToken stoppingToken)
        {
            CancellationTokenSource closeToken = new(
                TimeSpan.FromSeconds(Program.ServerConfig.CloseTimeout)
            );
            await _sendLock.WaitAsync(stoppingToken);
            try
            {
                await WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Close session",
                    closeToken.Token
                );
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task CloseOutputAsync(CancellationToken stoppingToken)
        {
            await CloseOutputAsyncWithStatus(
                WebSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                WebSocket.CloseStatusDescription ?? "close session",
                stoppingToken
            );
        }

        public async Task CloseOutputAsyncWithStatus(
            WebSocketCloseStatus status,
            string description,
            CancellationToken stoppingToken
        )
        {
            CancellationTokenSource closeToken = new(
                TimeSpan.FromSeconds(Program.ServerConfig.CloseTimeout)
            );
            await _sendLock.WaitAsync(stoppingToken);
            try
            {
                await WebSocket.CloseOutputAsync(status, description, closeToken.Token);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task SendAsync(string json, CancellationToken stoppingToken)
        {
            if (WebSocket.State is not WebSocketState.Open)
            {
                return;
            }

            await _sendLock.WaitAsync(stoppingToken);
            try
            {
                await WebSocket.SendAsync(
                    Encoding.UTF8.GetBytes(json).AsMemory(),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    stoppingToken
                );
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
