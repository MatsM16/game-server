using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Immutable;

namespace GameServer.Cli;

public sealed class DockerJob
{
    public string Image { get; init; } = "ubuntu:latest";
    public required DirectoryInfo Location { get; init; }
    public required string Name { get; init; }
    public required ImmutableList<string> Command { get; init; }
}

public sealed class DockerWorker(DockerClient docker)
{
    public async Task ExecuteAsync(DockerJob job, CancellationToken cancellationToken = default)
    {
        var container = await docker.Containers.CreateContainerAsync(new()
        {
            Name = $"{job.Name}_{Guid.NewGuid():n}",
            Image = job.Image,
            WorkingDir = "/data",
            Cmd = job.Command,
            HostConfig = new()
            {
                Mounts =
                [
                    new()
                    {
                        Type = "bind",
                        Source = job.Location.FullName,
                        Target = "/data"
                    }
                ],
                RestartPolicy = new()
                {
                    Name = RestartPolicyKind.No
                }
            },
            Labels = new Dictionary<string, string>()
            {
                ["com.docker.compose.project"] = "game-server-jobs"
            }
        }, cancellationToken);

        await docker.Containers.StartContainerAsync(container.ID, new(), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var inspect = await docker.Containers.InspectContainerAsync(container.ID, cancellationToken);
            if (inspect.State is not null && inspect.State.Status is "exited" or "dead")
                break;
            await Task.Delay(500, cancellationToken);
        }

        await docker.Containers.RemoveContainerAsync(container.ID, new() { Force = true }, cancellationToken);
    }
}
