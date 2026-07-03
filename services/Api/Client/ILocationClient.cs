using Api.Domain.Posts.Dto;

namespace Api.Client;

public interface ILocationClient
{
    public Task UpsertLocationForPostAsync(
        long postId,
        long userId,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default);

    public Task ClearLocationForPostAsync(
        long postId,
        long userId,
        CancellationToken cancellationToken = default);

    public Task ClearLocationForPostOnDeleteAsync(long postId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, PostLocationGetResponseDto>> GetLocationsByPostIdsAsync(
        IReadOnlyCollection<long> postIds,
        CancellationToken cancellationToken = default);

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default);

    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default);
}
