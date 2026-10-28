using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Directories.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    internal static class Program
    {
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        private static readonly ProjectDirectories _projectDir = ProjectDirectories.From(
            "com",
            "HitSZ-MistStudio",
            "MistForest"
        );
        internal static ServerConfig ServerConfig = new();

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
                ServerConfig = config;
            }
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(ServerConfig.Log.ParseLogLevel())
                .WriteTo.Console()
                .WriteTo.File(
                    ServerConfig.Log.Path,
                    rollingInterval: ServerConfig.Log.ParseRoll(),
                    retainedFileCountLimit: ServerConfig.Log.RetainCount
                )
                .CreateLogger();
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<GameServer>();
            builder.Services.AddHostedService(service => service.GetRequiredService<GameServer>());
            WebApplication app = builder.Build();
            app.UseWebSockets();
            _ = app.MapGet(
                "/ws",
                async context =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        CancellationToken stoppingToken = app.Lifetime.ApplicationStopping;
                        GameServer gameServer =
                            context.RequestServices.GetRequiredService<GameServer>();
                        using CancellationTokenSource connectionToken =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                context.RequestAborted,
                                stoppingToken
                            );
                        await gameServer.HandleClientAsync(context, connectionToken, stoppingToken);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    }
                }
            );
            Log.Information($"MistForest referee listening on ws://0.0.0.0:{ServerConfig.Port}/ws");
            app.Run($"http://0.0.0.0:{ServerConfig.Port}");
        }
    }
}
