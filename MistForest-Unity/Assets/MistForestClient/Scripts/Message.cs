using System;
using System.Text;
using System.Text.Json.Nodes;
using MistForest.Core.Protocol;
using Newtonsoft.Json;

namespace MistForest.Client.Message
{
    public record ClientMessage { }

    public sealed record Payload : ClientMessage
    {
        public ReadOnlyMemory<byte> Data { get; }

        private Payload(ReadOnlyMemory<byte> data) => Data = data;

        public static Payload New(ReadOnlyMemory<byte> data)
        {
            return new(data);
        }

        public string Type()
        {
            return JsonConvert
                .DeserializeObject<EnvelopeMsg>(Encoding.UTF8.GetString(Data.Span))!
                .Type;
        }
    }

    public sealed record ClientDisconnected : ClientMessage
    {
        public int CloseCode { get; }
        public string CloseStatusDescription { get; }

        private ClientDisconnected(int closeCode, string closeStatusDescription)
        {
            CloseCode = closeCode;
            CloseStatusDescription = closeStatusDescription;
        }

        public static ClientDisconnected New(int closeCode, string closeStatusDescription)
        {
            return new(closeCode, closeStatusDescription);
        }
    }

    public sealed record ClientError : ClientMessage
    {
        public int CloseCode { get; }
        public string Error { get; }

        private ClientError(int closeCode, string error)
        {
            CloseCode = closeCode;
            Error = error;
        }

        public static ClientError New(int closeCode, string error)
        {
            return new(closeCode, error);
        }
    }
}
