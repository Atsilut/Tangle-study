namespace Tangle.AspNetCore.Infrastructure;

public sealed class LazyService<T>(IServiceProvider serviceProvider)
    : Lazy<T>(() => serviceProvider.GetRequiredService<T>()) where T : class;
