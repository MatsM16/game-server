using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GameServer.Cli.Commands;

public class DeleteServer(DockerServerRuntime runtime) : AsyncCommand<DeleteServer.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SERVER>")]
        [Description("Name of the server.")]
        public string? Name { get; set; }

        [CommandOption("--soft")]
        [Description("Only delete the server listing, not the actual data.")]
        public bool Soft { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = await runtime.PickSingleServer(new Filter<GameServerStatus>().ByName(settings.Name), cancellationToken);
        if (!result.TrySuccess(out var server, out var exitCode))
            return exitCode;

        await AnsiConsole
            .Status()
            .StartAsync($"Deleting server...", _ => runtime.DeleteServerAsync(server, settings.Soft, cancellationToken));

        if (settings.Soft)
            AnsiConsole.MarkupLine("[green]Server removed from listing.[/]");
        else 
            AnsiConsole.MarkupLine("[green]Server deleted.[/]");

        return 0;
    }
}