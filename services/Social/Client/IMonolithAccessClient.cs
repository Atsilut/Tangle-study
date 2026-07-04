namespace Social.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(
        long userId,
        string notFoundMessage = "User not found",
        int statusCode = StatusCodes.Status400BadRequest,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);

    public Task<FriendsListVisibility> GetFriendsListVisibilityAsync(
        long userId,
        CancellationToken cancellationToken = default);
}

public enum FriendsListVisibility
{
    Public = 0,
    FriendsOnly = 1,
    Private = 2,
}
