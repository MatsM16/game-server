namespace GameServer.Cli;

public static class GameServerFilter
{
    public static Filter<GameServerStatus> ByGamePrefix(this Filter<GameServerStatus> filter, string? gameType)
    {
        return string.IsNullOrWhiteSpace(gameType) ? filter : filter.Add(server => server.Game.StartsWith(gameType, StringComparison.OrdinalIgnoreCase));
    }

    public static Filter<GameServerStatus> ByLocation(this Filter<GameServerStatus> filter, DirectoryInfo? folder)
    {
        return folder is null ? filter : filter.Add(server => new DirectoryInfo(server.Location).FullName.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase));
    }

    public static Filter<GameServerStatus> ByName(this Filter<GameServerStatus> filter, string? serverName)
    {
        return string.IsNullOrWhiteSpace(serverName) ? filter : filter.Add(server => server.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class Filter<T>
{
    private readonly List<Func<T, bool>> _filters = [];

    public static implicit operator Func<T, bool>(Filter<T> filter) => filter.Allowed;

    public Filter<T> Add(Func<T, bool> filter)
    {
        _filters.Add(filter);
        return this;
    }

    public bool Allowed(T server)
    {
        return _filters.Count is 0 || _filters.All(x => x(server));
    }

    public List<T> Allowed(IEnumerable<T> servers)
    {
        return servers.Where(this).ToList();
    }
}
