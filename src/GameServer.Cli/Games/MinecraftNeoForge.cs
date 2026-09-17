using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace GameServer.Cli.Games;

[DisplayName("minecraft.neoforge")]
public sealed class MinecraftNeoForge
{
    [Description("Minecraft version")]
    [Required]
    public required string Version { get; init; }

    [Description("NeoForge version")]
    public string? NeoForge { get; init; }
}

public sealed class MinecraftNeoForgeController(HttpClient http, DockerWorker worker) : GameServerController<MinecraftNeoForge>
{
    public override async Task<CreateDockerServerRequest> InstallServerAsync(MinecraftNeoForge server, CreateServerRequest request, CancellationToken cancellationToken)
    {
        var neoForgeVersions = await GetNeoForgeVersions(server.Version, cancellationToken);
        if (server.NeoForge is not null && !neoForgeVersions.Contains(server.NeoForge))
        {
            throw new InvalidOperationException($"NeoForge version {server.NeoForge} is not available for Minecraft {server.Version}.");
        }

        var neoForgeVersion = server.NeoForge ?? neoForgeVersions.Last();
        var installerJarUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
        await http.GetFileIfNotExistsAsync(installerJarUrl, request.File("server-installer.jar"), cancellationToken);
        await request.File("eula.txt").Text("eula=true");

        await worker.ExecuteAsync(new()
        {
            Image = "amazoncorretto:26",
            Location = request.Location,
            Name = $"install_neoforge_{neoForgeVersion}",
            Command = ["java", "-jar", "server-installer.jar", "--installServer"]
        }, cancellationToken);

        return new()
        {
            Image = "amazoncorretto:26",
            Entrypoint = ["./run.sh"]
        };
    }

    private async Task<List<string>> GetNeoForgeVersions(string minecraftVersion, CancellationToken cancellationToken)
    {
        var manifest = await http.GetFromJsonAsync<Manifest>("https://maven.neoforged.net/api/maven/versions/releases/net%2Fneoforged%2Fneoforge", cancellationToken)
            ?? throw new InvalidOperationException("Failed to fetch Minecraft version manifest.");

        var versions = manifest.Versions.Where(x => GetMinecraftVersion(x) == minecraftVersion).ToList();
        if (versions.Count is 0)
            throw new InvalidOperationException("NeoForge does not support Minecraft version " + minecraftVersion);

        return versions;
    }

    private static string GetMinecraftVersion(string neoForgeVersion)
    {
        var parts = neoForgeVersion.Split('.');
        if (parts.Length < 2)
            return neoForgeVersion;

        if (int.TryParse(parts[0], out var major) && major >= 26)
        {
            // 26.3.0.1-beta -> 26.3
            var version26 = $"{parts[0]}.{parts[1]}";

            // 26.1.2.109 -> 26.1.2
            if (parts.Length > 2 && parts[2] != "0")
                version26 += $".{parts[2]}";

            // 26.1.0.0-alpha.14+snapshot-11 -> 26.1-snapshot-11
            var snapshotParts = neoForgeVersion.Split('+');
            if (snapshotParts.Length > 1)
            {
                version26 += $"-{snapshotParts[1]}";
            }

            return version26;
        }

        // 21.1.250 -> 1.21.1
        return $"1.{parts[0]}.{parts[1]}";
    }

    record Manifest(List<string> Versions);
}
