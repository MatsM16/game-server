using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GameServer.Cli.Games;

[DisplayName("minecraft.forge")]
public sealed class MinecraftForge
{
    [Description("Minecraft version")]
    [Required]
    public required string Version { get; init; }

    [Description("Forge version")]
    public string? Forge { get; init; }
}

public sealed class MinecraftForgeController(HttpClient http, DockerWorker worker) : GameServerController<MinecraftForge>
{
    public override async Task<CreateDockerServerRequest> InstallServerAsync(MinecraftForge server, CreateServerRequest request, CancellationToken cancellationToken)
    {
        var installerUrl = await InstallerJarUrl(server.Version, server.Forge, cancellationToken);
        await http.GetFileIfNotExistsAsync(installerUrl, request.File("server-installer.jar"), cancellationToken);
        await request.File("eula.txt").Text("eula=true");

        await worker.ExecuteAsync(new()
        {
            Image = "amazoncorretto:26",
            Location = request.Location,
            Name = $"install_forge",
            Command = ["java", "-jar", "server-installer.jar", "--installServer"]
        }, cancellationToken);

        return new()
        {
            Image = "amazoncorretto:26",
            Entrypoint = ["./run.sh"]
        };
    }

    public async Task<string> InstallerJarUrl(string minecraftVersion, string? forgeVersion, CancellationToken cancellationToken = default)
    {
        var downloadPageResponse = await http.GetAsync($"https://files.minecraftforge.net/net/minecraftforge/forge/index_{minecraftVersion}.html", cancellationToken);
        if (downloadPageResponse.StatusCode is System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Forge is not available for Minecraft version {minecraftVersion}.");

        var downloadsPage = await downloadPageResponse.ReadAsHtmlAsync(cancellationToken);
        var forgeVersions = downloadsPage.DocumentNode.Descendants()
            .Where(x => x.HasClass("download-list"))
            .Select(x => x.Element("tbody")!)
            .SelectMany(x => x.Elements("tr"))
            .ToDictionary(
                x => x.Descendants().First(x => x.HasClass("download-version")).InnerText.Trim(),
                x => x.Descendants().First(x => x.HasClass("classifier-installer")).ParentNode!.GetAttributeValue("href", ""));

        if (forgeVersion is not null && !forgeVersions.ContainsKey(forgeVersion))
            throw new InvalidOperationException($"Forge version {forgeVersion} is not available for Minecraft version {minecraftVersion}.");

        forgeVersion ??= forgeVersions.Keys.OrderByDescending(Version.Parse).FirstOrDefault();

        var url = new Uri(forgeVersions[forgeVersion!]);

        var trueUrl = url.Query.Split('&')
            .Select(x => x.Split('='))
            .Where(x => x.Length == 2 && x[0] == "url")
            .Select(x => Uri.UnescapeDataString(x[1]))
            .FirstOrDefault();

        return trueUrl!;
    }

    record VersionContainer(string Version);
}