namespace Users.Infrastructure;

internal static class UsersHttpClientDefaults
{
    public static readonly TimeSpan OutboundTimeout = TimeSpan.FromSeconds(30);
}
