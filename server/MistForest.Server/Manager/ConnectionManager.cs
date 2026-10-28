using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using MistForest.Core.Utils;
using Serilog;

namespace MistForest.Server.Manager
{
    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _sessionsById = new();
        private readonly ConcurrentDictionary<WebSocket, PlayerSession> _sessionsBySocket = new();

        public PlayerSession? AddSession(string sessionId, Box<WebSocket> webSocket)
        {
            if (webSocket.TryTake() is not WebSocket webSocketItem)
            {
                return null;
            }
            PlayerSession session = PlayerSession.New(sessionId, webSocketItem);
            _sessionsBySocket[webSocketItem] = session;
            _sessionsById[sessionId] = session;
            return session;
        }

        public PlayerSession? GetById(string sessionId)
        {
            return _sessionsById.TryGetValue(sessionId, out PlayerSession? session)
                ? session
                : null;
        }

        public void RemoveBySocket(WebSocket webSocket)
        {
            if (_sessionsBySocket.TryRemove(webSocket, out PlayerSession? session))
            {
                _sessionsById.TryRemove(session.SessionId, out _);
                Log.Information($"Remove session {session.SessionId}");
            }
        }

        public void RemoveById(string sessionId)
        {
            if (_sessionsById.TryRemove(sessionId, out PlayerSession? session))
            {
                _sessionsBySocket.TryRemove(session.WebSocket, out _);
                Log.Information($"Remove session {session.SessionId}");
            }
        }

        public void AbortAll()
        {
            foreach (WebSocket webSocket in _sessionsBySocket.Keys)
            {
                if (webSocket.State is not WebSocketState.Closed or WebSocketState.Aborted)
                {
                    webSocket.Abort();
                }
            }
        }
    }
}
