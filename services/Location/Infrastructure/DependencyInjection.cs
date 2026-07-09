using Tangle.AspNetCore.Infrastructure;
using System.Reflection;

namespace Location.Infrastructure;

public static partial class DependencyInjection
{
    private static readonly List<(LayerType Layer, string Message)> _logs = [];

    public enum LayerType
    {
        Service,
        Repository
    }

    public static IServiceCollection AddCustomDependencies(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

        var assembly = Assembly.GetExecutingAssembly();

        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            var serviceAttr = type.GetCustomAttribute<ServiceAttribute>();
            if (serviceAttr is not null)
            {
                Register(services, type, serviceAttr.Lifetime, LayerType.Service);
                continue;
            }

            var repoAttr = type.GetCustomAttribute<RepositoryAttribute>();
            if (repoAttr is not null)
                Register(services, type, repoAttr.Lifetime, LayerType.Repository);
        }

        return services;
    }

    private static void Register(
        IServiceCollection services,
        Type implementation,
        ServiceLifetime lifetime,
        LayerType layer)
    {
        var interfaces = implementation.GetInterfaces();

        if (interfaces.Length > 0)
        {
            foreach (var i in interfaces)
            {
                var msg = $"[{layer}] {lifetime} {i.FullName} -> {implementation.FullName}";
                _logs.Add((layer, msg));

                services.Add(new ServiceDescriptor(i, implementation, lifetime));
            }
        }
        else
        {
            var msg = $"[{layer}] {lifetime} {implementation.FullName}";
            _logs.Add((layer, msg));

            services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
        }
    }

    public static void PrintLogs(ILogger logger, LayerType? filter = null)
    {
        var logs = filter is null
            ? _logs
            : _logs.Where(x => x.Layer == filter);

        foreach (var group in logs.GroupBy(x => x.Layer))
        {
            LogDependencyLayerHeader(logger, group.Key);

            foreach (var entry in group)
                LogDependencyRegistrationEntry(logger, entry.Message);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "===== {Layer} Layer =====")]
    private static partial void LogDependencyLayerHeader(ILogger logger, LayerType layer);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Message}")]
    private static partial void LogDependencyRegistrationEntry(ILogger logger, string message);
}
