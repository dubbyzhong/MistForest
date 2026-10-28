using System;
using System.Text.Json;
using MistForest.Core.Protocol;

namespace MistForest.Server
{
    public abstract record InboundMessage { }

    public sealed record ClientPayload : InboundMessage
    {
        public PlayerSession Session { get; set; }
        public ReadOnlyMemory<byte> Data { get; set; }

        private ClientPayload(PlayerSession session, ReadOnlyMemory<byte> data)
        {
            Session = session;
            Data = data;
        }

        public static ClientPayload New(PlayerSession session, ReadOnlyMemory<byte> data)
        {
            return new ClientPayload(session, data);
        }

        internal bool IsLobbyMessage()
        {
            return Type() is "createRoom";
        }

        public string Type()
        {
            return JsonSerializer.Deserialize<EnvelopeMsg>(Data.Span, Program.JsonOptions)!.Type;
        }
    }

    public sealed record ClientDisconnected : InboundMessage
    {
        public string SessionId;

        private ClientDisconnected(string sessionId) => SessionId = sessionId;

        public static ClientDisconnected New(string sessionId)
        {
            return new ClientDisconnected(sessionId);
        }
    }
}
