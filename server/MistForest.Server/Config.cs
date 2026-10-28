using MistForest.Server.Utils;
using Serilog;
using Serilog.Events;
using Tomlyn.Serialization;

namespace MistForest.Server
{
    public class ServerConfig
    {
        public int Port { get; set; } = 1437;
        public int GlobalMsgCapacity { get; set; } = 1024;

        [TomlInclude]
        private string _bufferSize { get; set; } = "4KiB";

        public int CloseTimeout { get; set; } = 5;

        public int BufferSize => ParseSize.Parse(_bufferSize) ?? 4096;
        public LogConfig Log { get; set; }

        public ServerConfig() => Log = new();

        public class LogConfig
        {
            public string Level { get; set; } = "Debug";
            public string Path { get; set; } = "./logs";
            public string Roll { get; set; } = "Day";
            public int RetainCount { get; set; } = 30;

            public LogEventLevel ParseLogLevel()
            {
                return Level.ToLowerInvariant() switch
                {
                    "verbose" => LogEventLevel.Verbose,
                    "debug" or "dbg" => LogEventLevel.Debug,
                    "information" or "info" or "inf" => LogEventLevel.Information,
                    "warning" or "warn" or "wrn" => LogEventLevel.Warning,
                    "error" or "err" => LogEventLevel.Error,
                    "fatal" => LogEventLevel.Fatal,
                    _ => LogEventLevel.Debug,
                };
            }

            public RollingInterval ParseRoll()
            {
                return Roll.ToLowerInvariant() switch
                {
                    "infinite" or "inf" => RollingInterval.Infinite,
                    "year" => RollingInterval.Year,
                    "month" => RollingInterval.Month,
                    "day" or "d" => RollingInterval.Day,
                    "hour" or "h" => RollingInterval.Hour,
                    "minute" or "min" => RollingInterval.Minute,
                    _ => RollingInterval.Day,
                };
            }
        }
    }
}
