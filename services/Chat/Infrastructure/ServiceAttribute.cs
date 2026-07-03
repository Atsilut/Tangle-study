namespace Chat.Global.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped) : Attribute
    {
        public ServiceLifetime Lifetime { get; } = lifetime;
    }
}
