using Spectre.Console.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Cli;

public static class ConsoleCancellationToken
{
    public static CancellationToken Create()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        return cts.Token;
    }
}

public static class SpectreConsoleServiceCollection
{
    public static ITypeRegistrar ToTypeRegistrar(this IServiceCollection services) => new ServiceCollectionTypeRegistar(services);

    public static CommandApp BuildCommandApp(this IServiceCollection services) => new CommandApp(services.ToTypeRegistrar());

    public static CommandApp<TDefaultCommand> BuildCommandApp<TDefaultCommand>(this IServiceCollection services) where TDefaultCommand : class, ICommand => new CommandApp<TDefaultCommand>(services.ToTypeRegistrar());
}

file class ServiceCollectionTypeRegistar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new ServiceProviderTypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}

file class ServiceProviderTypeResolver(IServiceProvider services) : ITypeResolver
{
    public object? Resolve(Type? type) => type is null ? null : services.GetService(type);
}