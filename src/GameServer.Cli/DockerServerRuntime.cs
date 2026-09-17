using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace GameServer.Cli;

[DebuggerDisplay("{Game}")]
public sealed class GameServerSchema
{
    /// <summary>
    /// Unique identifier for the server type. Like "minecraft.vanilla" or "unturned".
    /// </summary>
    public required string Game { get; init; }

    public IReadOnlyList<Setting> Settings { get; init; } = [];

    [DebuggerDisplay("{Type} {Name}")]
    public sealed class Setting
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public bool Required { get; init; }
        public SettingType Type { get; init; } = SettingType.String;
    }

    public enum SettingType
    {
        String,
    }
}

[DebuggerDisplay("{Schema}")]
public sealed class CreateServerRequest
{
    public required GameServerSchema Schema { get; init; }
    public required DirectoryInfo Location { get; init; }
    public required Dictionary<string, string> Settings { get; init; }
}

[DebuggerDisplay("{Game} {Location}")]
public class GameServer
{
    public required string Game { get; init; }
    public required string Location { get; init; }
}

public sealed class CreateDockerServerRequest
{
    public required string Image { get; init; }
    public required string[] Entrypoint { get; init; }
}

public sealed class GameServerStatus : GameServer
{
    public required bool Running { get; init; }
    public string Name => Location.Split(Path.DirectorySeparatorChar)[^1];
}

public sealed class DockerServerRuntime(DockerClient docker, IEnumerable<IGameServerController> controllers)
{
    public async Task<List<GameServerStatus>> ListServersAsync(CancellationToken cancellationToken = default)
    {
        await ThrowIfDockerIsNotRunning(cancellationToken);

        var containers = await docker.Containers.ListContainersAsync(new()
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>()
            {
                ["label"] = new Dictionary<string, bool>()
                {
                    [$"game-server=true"] = true,
                }
            }
        }, cancellationToken);

        return [.. containers.Select(c => new GameServerStatus()
        {
            Game = c.Labels["game-server.game"],
            Location = c.Labels["game-server.folder"],
            Running = c.State is "running"
        })];
    }

    public async Task<List<GameServerSchema>> ListServerSchemasAsync(CancellationToken cancellationToken = default)
    {
        await ThrowIfDockerIsNotRunning(cancellationToken);

        var bag = new ConcurrentBag<GameServerSchema>();
        await Parallel.ForEachAsync(controllers, cancellationToken, async (ctrl, ct) =>
        {
            foreach (var schema in await ctrl.SchemasAsync(ct))
            {
                bag.Add(schema);
            }
        });
        return bag.OrderBy(x => x.Game).ToList();
    }

    public async Task CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ThrowIfDockerIsNotRunning(cancellationToken);

        var controller = controllers.FirstOrDefault(c => c.CanCreateServer(request.Schema))
            ?? throw new InvalidOperationException("No controller found for the given server options.");

        request.Location.Create();

        var server = await controller.CreateServerAsync(request, cancellationToken);

        var existing = await GetServerContainer(request.Location, cancellationToken);
        if (existing is not null)
        {
            if (existing.State is "running")
                throw new InvalidOperationException("Server already running.");

            await docker.Containers.StopContainerAsync(existing.ID, new(), cancellationToken);
            await docker.Containers.RemoveContainerAsync(existing.ID, new() { Force = true }, cancellationToken);
        }

        var container = await docker.Containers.CreateContainerAsync(new()
        {
            Image = server.Image,
            Name = $"{request.Schema.Game}-{Random.Shared.Next():x}",
            HostConfig = new()
            {
                Mounts = [
                    new() {
                        Type = "bind",
                        Source = request.Location.FullName,
                        Target = "/data"
                    }
                ],
                RestartPolicy = new()
                {
                    Name = RestartPolicyKind.UnlessStopped
                },
                NetworkMode = "host"
            },
            Labels = new Dictionary<string, string>()
            {
                ["game-server"] = "true",
                ["game-server.game"] = request.Schema.Game,
                ["game-server.folder"] = request.Location.FullName,
                ["com.docker.compose.project"] = "game-servers"
            },
            WorkingDir = "/data",
            Entrypoint = server.Entrypoint,
        }, cancellationToken);
    }

    public async Task StartServerAsync(GameServer server, CancellationToken cancellationToken = default)
    {
        await ThrowIfDockerIsNotRunning(cancellationToken);

        var container = await GetServerContainer(server, cancellationToken);
        if (container is null) throw new InvalidOperationException("Server does not exist.");
        if (container.State is "running") return;

        await docker.Containers.StartContainerAsync(container.ID, new(), cancellationToken);
    }

    public async Task StopServerAsync(GameServer server, CancellationToken cancellationToken = default)
    {
        await ThrowIfDockerIsNotRunning(cancellationToken);

        var container = await GetServerContainer(server, cancellationToken);
        if (container is null) return;
        if (container.State is not "running") return;

        await docker.Containers.StopContainerAsync(container.ID, new(), cancellationToken);
    }

    public async Task DeleteServerAsync(GameServer server, bool keepData, CancellationToken cancellationToken = default)
    {
        await ThrowIfDockerIsNotRunning(cancellationToken);

        var container = await GetServerContainer(server, cancellationToken);
        if (container is null) throw new InvalidOperationException("Server does not exist.");
        if (container.State is "running") throw new InvalidOperationException("Server is running. Please stop server before deleting it.");

        await docker.Containers.StopContainerAsync(container.ID, new(), cancellationToken);
        await docker.Containers.RemoveContainerAsync(container.ID, new(), cancellationToken);

        if (!keepData)
        {
            var location = new DirectoryInfo(server.Location);
            if (location.Exists)
                location.Delete(recursive: true);
        }
    }

    private Task<ContainerListResponse?> GetServerContainer(GameServer server, CancellationToken cancellationToken)
    {
        var location = new DirectoryInfo(server.Location);
        return GetServerContainer(location, cancellationToken);
    }

    private async Task<ContainerListResponse?> GetServerContainer(DirectoryInfo installLocation, CancellationToken cancellationToken)
    {
        var containers = await docker.Containers.ListContainersAsync(new()
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>()
            {
                ["label"] = new Dictionary<string, bool>()
                {
                    [$"game-server=true"] = true,
                    [$"game-server.folder={installLocation.FullName}"] = true
                }
            }
        }, cancellationToken);

        return containers.FirstOrDefault();
    }

    private async Task ThrowIfDockerIsNotRunning(CancellationToken cancellationToken)
    {
        try
        {
            await docker.System.PingAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Docker is not running. Please start Docker.");
        }
        catch (DockerApiException ex) when (ex.StatusCode is HttpStatusCode.ServiceUnavailable)
        {
            throw new InvalidOperationException("Docker is paused. Please resume Docker.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Docker is unavailable.", ex);
        }
    }
}

public interface IGameServerController
{
    Task<List<GameServerSchema>> SchemasAsync(CancellationToken cancellationToken = default);

    bool CanCreateServer(GameServerSchema schema);

    Task<CreateDockerServerRequest> CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default);
}

public abstract class GameServerController<TSettings> : IGameServerController
{
    private static readonly string _game = typeof(TSettings).GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? typeof(TSettings).Name.ToLowerInvariant();
    private static readonly Dictionary<string, PropertyInfo> _properties = CreatePropertyMap();
    private static readonly GameServerSchema _schema = CreateSchema();

    public bool CanCreateServer(GameServerSchema schema) => _game.Equals(schema.Game, StringComparison.OrdinalIgnoreCase);

    public abstract Task<CreateDockerServerRequest> InstallServerAsync(TSettings options, CreateServerRequest request, CancellationToken cancellationToken);

    public Task<CreateDockerServerRequest> CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default)
    {
        var options = Activator.CreateInstance<TSettings>();

        foreach (var (key, prop) in _properties)
        {
            if (request.Settings.TryGetValue(key, out var value))
            {
                var converter = TypeDescriptor.GetConverter(prop.PropertyType);
                prop.SetValue(options, converter.ConvertFromInvariantString(value));
            }
        }

        return InstallServerAsync(options, request, cancellationToken);
    }

    public Task<List<GameServerSchema>> SchemasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<List<GameServerSchema>>([_schema]);
    }

    private static Dictionary<string, PropertyInfo> CreatePropertyMap()
    {
        return typeof(TSettings).GetProperties().Where(x => x.CanWrite).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static GameServerSchema CreateSchema() => new()
    {
        Game = _game,
        Settings = CreatePropertyMap().Select(x => new GameServerSchema.Setting()
        {
            Name = x.Key,
            Type = GameServerSchema.SettingType.String,
            Required = x.Value.GetCustomAttributes(typeof(RequiredAttribute), true).Any(),
            Description = x.Value.GetCustomAttributes(typeof(DescriptionAttribute), true)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description
        }).ToList()
    };
}
