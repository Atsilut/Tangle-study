namespace Users.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RepositoryAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped) : Attribute
    {
        public ServiceLifetime Lifetime { get; } = lifetime;
    }
}
