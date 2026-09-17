using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GameServer.Cli.Commands;

public class StopServer(DockerServerRuntime runtime) : AsyncCommand<StopServer.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SERVER>")]
        [Description("Name of the server.")]
        public required string Name { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = await runtime.PickSingleServer(new Filter<GameServerStatus>().ByName(settings.Name), cancellationToken);
        if (!result.TrySuccess(out var server, out var exitCode))
            return exitCode;

        if (!server.Running)
        {
            AnsiConsole.MarkupLine("Server is not running.");
            return 0;
        }

        await AnsiConsole.Status().StartAsync("Stopping server...", _ => runtime.StopServerAsync(server, cancellationToken));

        AnsiConsole.MarkupLine("[green]Server stopped.[/]");

        return 0;
    }
}