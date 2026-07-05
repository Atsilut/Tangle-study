namespace Community.Client;

public interface IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);

    public Task<long?> GetUserIdByNicknameAsync(string nickname, CancellationToken cancellationToken = default);
}
