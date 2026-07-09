using Group.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.Http;

namespace Group.Client;

internal sealed class HttpUserClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UsersOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpUserClient)), IUserClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public async Task EnsureUserExistsAsync(
        long userId,
        string notFoundMessage = "User not found",
        int statusCode = StatusCodes.Status400BadRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var path = notFoundMessage == "Authentication failed"
                ? $"internal/users/{userId}/exists"
                : $"internal/users/{userId}/validate";
            await PostNoContentAsync(path, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            throw new EntityNotFoundException(notFoundMessage, statusCode);
        }
        catch (ArgumentException)
        {
            throw new EntityNotFoundException(notFoundMessage, statusCode);
        }
    }

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<long, string>();

        var payload = await PostJsonAsync<InternalAccessNicknamesResponseDto>(
            "internal/users/nicknames",
            new InternalAccessUserIdsRequestDto(ids),
            cancellationToken)
            ?? throw new InvalidOperationException("Users nicknames response was empty.");

        return payload.Nicknames.ToDictionary(entry => entry.UserId, entry => entry.Nickname);
    }
}
