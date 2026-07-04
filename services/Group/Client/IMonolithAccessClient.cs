namespace Group.Client;

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
}
