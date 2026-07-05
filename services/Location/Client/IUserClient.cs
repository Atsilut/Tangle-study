namespace Location.Client;

public interface IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);
}
