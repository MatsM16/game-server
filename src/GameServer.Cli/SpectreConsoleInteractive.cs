using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;

namespace GameServer.Cli;

public static class SpectreConsoleInteractive
{
    public static async Task<int> RunInteractiveAsync(this CommandApp app, CancellationToken cancelToken)
    {
        var appName = GetApplicationName(app);

        Console.Title = $"Interactive {appName}";
        AnsiConsole.MarkupLine("[dim]Running in interactive test mode.[/]");
        AnsiConsole.MarkupLine("[dim]Type 'clear' to clear the screen, 'exit' to close the program.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var prompt = appName is null ? "[dim]>[/]" : $"[dim]> {appName}[/]";
            var newArgsAsString = await AnsiConsole.AskAsync<string>(prompt, cancelToken);

            if (newArgsAsString is "clear" or "cls")
            {
                AnsiConsole.Clear();
                continue;
            }
            if (newArgsAsString is "exit" or "quit")
            {
                return 0;
            }

            var newArgs = newArgsAsString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            await app.RunAsync(newArgs, cancelToken);
        }
    }

    private static string? GetApplicationName(this CommandApp app)
    {
        var config = typeof(CommandApp).GetMethod("GetConfigurator", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(app, []);
        var settings = config?.GetType().GetProperty("Settings")?.GetValue(config);
        return settings?.GetType().GetProperty("ApplicationName")?.GetValue(settings) as string;
    }
}
