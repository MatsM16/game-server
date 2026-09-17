using Docker.DotNet;
using GameServer.Cli;
using GameServer.Cli.Commands;
using GameServer.Cli.Games;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System.Diagnostics;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var services = new ServiceCollection()
    .AddSingleton(new HttpClient())
    .AddSingleton(new DockerClientConfiguration().CreateClient())
    .AddSingleton<DockerWorker>()
    .AddSingleton<DockerServerRuntime>();

// Controllers
services.AddSingleton<IGameServerController, MinecraftVanillaController>();
services.AddSingleton<IGameServerController, MinecraftNeoForgeController>();
services.AddSingleton<IGameServerController, MinecraftFabricController>();
services.AddSingleton<IGameServerController, MinecraftForgeController>();
services.AddSingleton<IGameServerController, SteamController>();

var app = services.BuildCommandApp();
app.Configure(config =>
{
    config.SetApplicationName("gameserver");
    config.AddCommand<ListServers>("list").WithAlias("ls").WithDescription("List all game servers");
    config.AddCommand<StartServer>("start").WithDescription("Start a game server");
    config.AddCommand<StopServer>("stop").WithDescription("Stop a game server");
    config.AddCommand<NewServer>("new").WithDescription("Create a new game server").WithExample(["new", "minecraft.fabric", "./mc-server", "--version", "1.20.1"]);
    config.AddCommand<DeleteServer>("delete").WithDescription("Delete a game server");
    config.AddCommand<SearchGames>("games").WithDescription("Search for supported games");
    config.AddCommand<AddServer>("add").WithDescription("Add an existing game server");
});

var cancelToken = ConsoleCancellationToken.Create();

if (Debugger.IsAttached)
    return await app.RunInteractiveAsync(cancelToken);

return await app.RunAsync(args, cancelToken);

