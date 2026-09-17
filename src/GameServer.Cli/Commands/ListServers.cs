using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GameServer.Cli.Commands;

public class ListServers(DockerServerRuntime runtime) : AsyncCommand<ListServers.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-g|--game")]
        [Description("Filters the game servers by game type.")]
        public string? GameType { get; set; }

        [CommandOption("-f|--folder")]
        [Description("Filters the game servers by folder.")]
        public DirectoryInfo? Folder { get; set; }

        [CommandOption("-n|--name")]
        [Description("Filters the game servers by name.")]
        public string? Name { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var servers = await AnsiConsole
            .Status()
            .StartAsync("Listing servers...", _ => runtime.ListServersAsync(cancellationToken));

        if (servers.Count is 0)
        {
            AnsiConsole.MarkupLine("No game servers.");
            return 0;
        }

        var filter = new Filter<GameServerStatus>()
            .ByName(settings.Name)
            .ByGamePrefix(settings.GameType)
            .ByLocation(settings.Folder);

        var filtered = filter.Allowed(servers);
        if (filtered.Count is 0)
        {
            AnsiConsole.MarkupLine("[dim]Showing none of {1} servers[/]", filtered.Count, servers.Count);
            return 0;
        }

        if (filtered.Count != servers.Count)
        {
            AnsiConsole.MarkupLine("[dim]Showing {0} of {1} servers[/]", filtered.Count, servers.Count);
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Showing all {0} servers[/]", servers.Count);
        }
        AnsiConsole.Write(filtered.ToConsoleTable());

        return 0;
    }
}
