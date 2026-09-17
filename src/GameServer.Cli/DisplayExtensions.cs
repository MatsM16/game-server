using Spectre.Console;

namespace GameServer.Cli;

public static class DisplayExtensions
{
    public static Table ToConsoleTable(this IEnumerable<GameServerStatus> servers)
    {
        var table = new Table();
        table.AddColumns("", "Name", "Game", "Path");
        foreach (var server in servers)
        {
            var style = server.Running ? Style.Plain : Style.Parse("dim");
            var relativePathToWorkingDirectory = "./" + Path.GetRelativePath(Environment.CurrentDirectory, server.Location);
            table.AddRow(
                new Text(server.Running ? "🟢" : "🔴", style),
                new Text(server.Name, style),
                new Text(server.Game, style),
                new TextPath(relativePathToWorkingDirectory));
        }
        return table;
    }

    public static async Task<Result<GameServerStatus, int>> PickSingleServer(this DockerServerRuntime runtime, Filter<GameServerStatus> filter, CancellationToken cancellationToken = default)
    {
        var servers = await AnsiConsole
            .Status()
            .StartAsync("Listing servers...", _ => runtime.ListServersAsync(cancellationToken));

        return servers.PickSingle(filter);
    }

    public static Result<GameServerStatus, int> PickSingle(this IEnumerable<GameServerStatus> servers, Filter<GameServerStatus> filter)
    {
        var list = servers.ToList();
        if (list.Count is 0)
        {
            AnsiConsole.MarkupLine("[red]No servers exist.[/]");
            return 0;
        }

        var filtered = filter.Allowed(list);
        if (filtered.Count is 0)
        {
            AnsiConsole.MarkupLine("[red]Could not find server.[/]");
            return 0;
        }

        if (filtered.Count > 1)
        {
            AnsiConsole.MarkupLine("[red]Multiple servers match the filter![/]");
            AnsiConsole.Write(filtered.ToConsoleTable());
            AnsiConsole.MarkupLine("[red]Please make the filter more specific.[/]");
            return 0;
        }

        return filtered[0];
    }
}
