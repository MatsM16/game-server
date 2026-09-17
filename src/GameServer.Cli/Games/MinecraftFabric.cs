using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace GameServer.Cli.Games;

[DisplayName("minecraft.fabric")]
public sealed class MinecraftFabric
{
    [Description("Minecraft version")]
    [Required]
    public required string Version { get; init; }

    [Description("Loader version")]
    public string? Loader { get; init; }

    [Description("Installer version")]
    public string? Installer { get; init; }
}

public sealed class MinecraftFabricController(HttpClient http) : GameServerController<MinecraftFabric>
{
    public override async Task<CreateDockerServerRequest> InstallServerAsync(MinecraftFabric server, CreateServerRequest request, CancellationToken cancellationToken)
    {
        var jarUrl = await ServerJarUrl(server.Version, server.Loader, server.Installer, cancellationToken);
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

    public async Task<string> ServerJarUrl(string minecraftVersion, string? loaderVersion, string? installerVersion, CancellationToken cancellationToken = default)
    {
        var getMinecraftVersions = GetMinecraftVersions(cancellationToken);
        var getLoaderVersions = GetLoaderVersions(cancellationToken);
        var getInstallerVersions = GetInstallerVersions(cancellationToken);

        await Task.WhenAll(getMinecraftVersions, getLoaderVersions, getInstallerVersions);

        if (!getMinecraftVersions.Result.Contains(minecraftVersion))
            throw new InvalidOperationException($"Fabric is not available for Minecraft version {minecraftVersion}.");

        if (loaderVersion is not null && !getLoaderVersions.Result.Contains(loaderVersion))
            throw new InvalidOperationException($"Fabric loader version {loaderVersion} does not exist.");

        if (installerVersion is not null && !getInstallerVersions.Result.Contains(installerVersion))
            throw new InvalidOperationException($"Fabric installer version {installerVersion} does not exist.");

        loaderVersion ??= getLoaderVersions.Result.First();
        installerVersion ??= getInstallerVersions.Result.First();

        return $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{loaderVersion}/{installerVersion}/server/jar";
    }

    private async Task<List<string>> GetInstallerVersions(CancellationToken cancellationToken = default)
    {
        var manifest = await http.GetFromJsonAsync<List<VersionContainer>>("https://meta.fabricmc.net/v2/versions/installer", cancellationToken)
            ?? throw new InvalidOperationException("Failed to fetch Fabric installers.");

        return manifest.Select(m => m.Version).ToList();
    }

    private async Task<List<string>> GetLoaderVersions(CancellationToken cancellationToken = default)
    {
        var manifest = await http.GetFromJsonAsync<List<VersionContainer>>("https://meta.fabricmc.net/v2/versions/loader", cancellationToken)
            ?? throw new InvalidOperationException("Failed to fetch Fabric loaders.");

        return manifest.Select(m => m.Version).ToList();
    }

    private async Task<List<string>> GetMinecraftVersions(CancellationToken cancellationToken = default)
    {
        var manifest = await http.GetFromJsonAsync<List<VersionContainer>>("https://meta.fabricmc.net/v2/versions/game", cancellationToken)
            ?? throw new InvalidOperationException("Failed to fetch Minecraft versions.");

        return manifest.Select(m => m.Version).ToList();
    }

    record VersionContainer(string Version);
}