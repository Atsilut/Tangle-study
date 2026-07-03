using Chat.Client;
using Chat.Config;

namespace Chat.Infrastructure;

public static class MonolithServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMonolithAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MonolithOptions>(configuration.GetSection(MonolithOptions.SectionName));
        var options = configuration.GetSection(MonolithOptions.SectionName).Get<MonolithOptions>() ?? new MonolithOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<IMonolithAccessClient, UnconfiguredMonolithAccessClient>();
            return services;
        }

        services.AddHttpClient(nameof(HttpMonolithAccessClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IMonolithAccessClient, HttpMonolithAccessClient>();

        return services;
    }
}

internal sealed class UnconfiguredMonolithAccessClient : IMonolithAccessClient
{
    private static InvalidOperationException NotConfigured() =>
        new("Monolith:BaseUrl is not configured. Set it to the monolith API base URL for access checks.");

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureUsersExistAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureFriendshipExistsForUserPairAsync(long otherUserId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureNoBlockBetweenUsersAsync(long otherUserId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureGroupExistsAsync(long groupId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureCallerIsGroupMemberAsync(long groupId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureGroupMembersAsync(
        long groupId,
        IReadOnlyCollection<long> userIds,
        string membersErrorMessage,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
