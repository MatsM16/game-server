namespace GameServer.Cli.Games;

public sealed class SteamController(DockerWorker worker) : IGameServerController
{
    private static readonly List<GameInfo> _games =
    [
        new("unturned", 1110390, "./ServerHelper.sh"),
        new("conanexiles", 443030, "./ConanSandboxServer.sh -log"),
        new("arma3", 233780, "./Arma3Server"),
        new("arma.reforger", 1874900, "./ArmaReforgerServer"),
        new("palworld", 2394010, "./PalServer.sh -useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS"),
    ];

    public bool CanCreateServer(GameServerSchema schema)
    {
        return _games.Any(x => x.Name.Equals(schema.Game));
    }

    public async Task<CreateDockerServerRequest> CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default)
    {
        var game = _games.First(x => x.Name.Equals(request.Schema.Game));

        await worker.ExecuteAsync(new()
        {
            Name = $"install_{game.Name}",
            Image = "steamcmd/steamcmd:latest",
            Location = request.Location,
            Command = [
                "+@sSteamCmdForcePlatformType linux",
                "+force_install_dir /data",
                "+login anonymous",
                $"+app_update {game.AppId}",
                "+validate",
                "+quit"
            ]
        }, cancellationToken);

        return new()
        {
            Image = game.BaseImage,
            Entrypoint = [game.Entrypoint]
        };
    }

    public Task<List<GameServerSchema>> SchemasAsync(CancellationToken cancellationToken = default)
    {
        var schemas = _games.Select(x => new GameServerSchema
        {
            Game = x.Name,
        }).ToList();

        return Task.FromResult(schemas);
    }

    record GameInfo(string Name, int AppId, string Entrypoint, string BaseImage = "ubuntu:latest");
}