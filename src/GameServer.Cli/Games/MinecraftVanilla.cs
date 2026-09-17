using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace GameServer.Cli.Games;

[DisplayName("minecraft.vanilla")]
public sealed class MinecraftVanilla
{
    [Description("Minecraft version")]
    [Required]
    public required string Version { get; init; }
}

public sealed class MinecraftVanillaController(HttpClient http) : GameServerController<MinecraftVanilla>
{
    public override async Task<CreateDockerServerRequest> InstallServerAsync(MinecraftVanilla server, CreateServerRequest request, CancellationToken cancellationToken)
    {
        var jarUrl = await FindServerJarUrl(server, cancellationToken);
        await http.GetFileIfNotExistsAsync(jarUrl, request.File("server.jar"), cancellationToken);
        await request.File("eula.txt").Text("eula=true");
        await request.File("jvm_args.txt").Text("""
            # Argument for the Java Virtual Machine (JVM)
            -Xmx4G
            -Xms1G
            """);

        return new()
        {
            Image = "amazoncorretto:26",
            Entrypoint = ["java", "@jvm_args.txt", "-jar", "server.jar", "nogui"]
        };
    }

    private async Task<string> FindServerJarUrl(MinecraftVanilla server, CancellationToken cancellationToken)
    {
        var manifest = await http.GetFromJsonAsync<MinecraftManifest>("https://launchermeta.mojang.com/mc/game/version_manifest.json", cancellationToken)
            ?? throw new InvalidOperationException("Failed to fetch Minecraft version manifest.");

        var version = manifest.Versions.FirstOrDefault(x => string.Equals(x.Id, server.Version))
            ?? throw new InvalidOperationException("Minecraft version not found.");

        var versionManifest = await http.GetFromJsonAsync<VersionManifest>(version.Url, cancellationToken)
            ?? throw new InvalidOperationException("Minecraft version found, but could not find server.jar download.");

        return versionManifest.Downloads.Server.Url;
    }

    record MinecraftManifestVersion(string Id, string Type, string Url);
    record MinecraftManifest(List<MinecraftManifestVersion> Versions);

    record VersionManifest(VersionManifestDownloads Downloads);
    record VersionManifestDownloads(VersionManifestDownload Server);
    record VersionManifestDownload(string Sha1, string Url, long Size);
}


