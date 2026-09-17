using Spectre.Console;
using Spectre.Console.Cli;

namespace GameServer.Cli.Commands;

public class SearchGames(DockerServerRuntime runtime) : AsyncCommand<SearchGames.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[query]")]
        public string? Query { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var games = await AnsiConsole
            .Status()
            .StartAsync("Listing games...", _ => runtime.ListServerSchemasAsync(cancellationToken));

        if (!games.Any())
        {
            AnsiConsole.MarkupLine("[red]No games are supported. This is bad![/]");
            return 1;
        }

        var filtered = string.IsNullOrWhiteSpace(settings.Query)
            ? games
            : games.Where(g => g.Game.Contains(settings.Query, StringComparison.OrdinalIgnoreCase));

        if (!filtered.Any())
        {
            AnsiConsole.MarkupLine("[red]Found no supported games matching the query.[/]");
            return 1;
        }

        var table = new Table()
            .ShowRowSeparators()
            .MinimalBorder()
            .MinimalHeavyHeadBorder()
            .BorderStyle(Style.Parse("dim"));

        table.AddColumns("Game", "Required settings", "Optional settings");

        var headerStyle = Style.Parse("bold");
        var descriptionStyle = Style.Parse("dim");
        foreach (var game in filtered)
        {
            var requiredSettings = new Rows(game.Settings.Where(x => x.Required).Select(x => new Columns(new Text(x.Name), new Text(x.Description ?? "", descriptionStyle))));
            var optionalSettings = new Rows(game.Settings.Where(x => !x.Required).Select(x => new Columns(new Text(x.Name), new Text(x.Description ?? "", descriptionStyle))));
            table.AddRow(new Text(game.Game), requiredSettings, optionalSettings);
        }

        AnsiConsole.MarkupLine("[dim]Showing {0} of {1} supported games.[/]", filtered.Count(), games.Count());
        AnsiConsole.Write(table);

        return 0;
    }
}
