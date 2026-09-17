using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GameServer.Cli.Commands;

public class NewServer(DockerServerRuntime runtime) : AsyncCommand<NewServer.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<GAME>")]
        [Description("Game to make a server for.")]
        public required string GameType { get; set; }

        [CommandArgument(1, "<PATH>")]
        [Description("Where to put the server files.")]
        public required DirectoryInfo Path { get; set; }

        [CommandOption("--overwrite")]
        [Description("If a server already exists in the folder, overwrite any colliding files.")]
        public bool Overwrite { get; set; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var servers = await runtime.ListServersAsync(cancellationToken);

        var inSameFolder = new Filter<GameServerStatus>().ByLocation(settings.Path).Allowed(servers);
        if (inSameFolder.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]A server is already created for the same folder.[/]");
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

        var options = new Dictionary<string, string>();

        var additionalSettings = context.Remaining.Parsed.ToDictionary(x => x.Key.TrimStart('-'), x => x.Last()!, StringComparer.OrdinalIgnoreCase);

        var missingRequired = schema.Settings
            .Where(x => x.Required)
            .Where(x => !additionalSettings.ContainsKey(x.Name))
            .ToList();

        if (missingRequired.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]Missing {0} required option(s):[/]", missingRequired.Count);
            foreach (var setting in missingRequired)
            {
                AnsiConsole.MarkupLine("  --{0} [dim]{1}[/]", setting.Name.ToLowerInvariant(), setting.Description ?? "");
            }
            return -1;
        }

        foreach (var setting in schema.Settings)
        {
            if (additionalSettings.TryGetValue(setting.Name, out var value))
            {
                options[setting.Name] = value;
                additionalSettings.Remove(setting.Name);
            }
        }

        if (additionalSettings.Count > 0)
            AnsiConsole.MarkupLine("[orange]{0} unknown options were ignored: {1}[/]", additionalSettings.Count, string.Join(", ", additionalSettings.Keys));

        if (settings.Path.Exists && settings.Path.EnumerateFiles().Any() && !settings.Overwrite)
        {
            AnsiConsole.MarkupLine("[red]The folder is not empty. Use --overwrite to overwrite any colliding files.[/]");
            return -1;
        }

        await runtime.CreateServerAsync(new CreateServerRequest()
        {
            Schema = schema,
            Location = settings.Path,
            Settings = options
        }, cancellationToken);

        AnsiConsole.MarkupLine("[green]Server created.[/]");

        return 0;
    }
}