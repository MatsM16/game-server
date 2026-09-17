using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GameServer.Cli.Commands;

public class AddServer(DockerServerRuntime runtime) : AsyncCommand<AddServer.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<GAME>")]
        [Description("Game to make a server for.")]
        public required string GameType { get; set; }

        [CommandArgument(1, "<PATH>")]
        [Description("Where to put the server files.")]
        public required DirectoryInfo Path { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var servers = await runtime.ListServersAsync(cancellationToken);

        var folderProblem = !settings.Path.Exists ? "does not exist" : !settings.Path.EnumerateFiles().Any() ? "is empty" : null;
        if (folderProblem is not null)
        {
            AnsiConsole.MarkupLine($"[red]The folder '{settings.Path.FullName}' {folderProblem}.[/]");
            AnsiConsole.MarkupLine("[dim]If you intended to create a new server, use [underline]gameserver new <GAME> <PATH>[/] instead.[/]");
            return -1;
        }

        var inSameFolder = new Filter<GameServerStatus>().ByLocation(settings.Path).Allowed(servers);
        if (inSameFolder.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]Server at that folder is already added.[/]");
            AnsiConsole.Write(inSameFolder.ToConsoleTable());
            return -1;
        }

        var schemas = await runtime.ListServerSchemasAsync(cancellationToken);
        var schema = schemas.FirstOrDefault(x => x.Game.Equals(settings.GameType, StringComparison.OrdinalIgnoreCase));
        if (schema is null)
        {
            AnsiConsole.MarkupLine($"[red]Could not find game server '{settings.GameType}'.[/]");

            var candidates = schemas.Where(x => x.Game.Contains(settings.GameType, StringComparison.OrdinalIgnoreCase)).Select(x => x.Game).ToList();
            if (candidates.Count > 0)
                AnsiConsole.MarkupLine($"[dim]Did you mean {string.Join(", ", candidates)}[/]");
            else
                AnsiConsole.MarkupLine($"[dim]Supported games are {string.Join(", ", schemas.Select(x => x.Game))}[/]");
            return -1;
        }

        AnsiConsole.MarkupLine("[red]Command is not implemented.[/]");
        return -1;
    }
}