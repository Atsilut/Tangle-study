using Community.Client;
using Community.Db;
using Community.Dto;
using Community.Entities;
using Tangle.AspNetCore.Exceptions;
using Community.Infrastructure;
using Community.Repository;
using Tangle.AspNetCore.Auth;
using Tangle.AspNetCore.Db;

namespace Community.Service;

[Service]
public class PostService(
    IPostRepository repo,
    CommunityDbContext db,
    Lazy<CommentService> commentService,
    IMediaClient mediaClient,
    ILocationClient locationClient,
    CurrentUserAccessor currentUser,
    IUserClient userClient,
    ISocialClient socialClient,
    IGroupClient groupClient,
    ILogger<PostService> logger)
{
    private readonly IPostRepository _repo = repo;
    private readonly CommunityDbContext _db = db;
    private readonly Lazy<CommentService> _commentService = commentService;
    private readonly IMediaClient _mediaClient = mediaClient;
    private readonly ILocationClient _locationClient = locationClient;
    private readonly CurrentUserAccessor _currentUser = currentUser;
    private readonly IUserClient _userClient = userClient;
    private readonly ISocialClient _socialClient = socialClient;
    private readonly IGroupClient _groupClient = groupClient;
    private readonly ILogger<PostService> _logger = logger;

    private Task<List<Post>> FilterPostsByBlockAsync(long? viewerUserId, List<Post> posts, CancellationToken cancellationToken = default) =>
        MutualBlockFilter.FilterByMutualBlockAsync(
            viewerUserId,
            posts,
            p => p.AuthorUserId,
            (viewerId, authorIds, ct) => _socialClient.GetMutuallyBlockedUserIdsAsync(viewerId, authorIds, ct),
            cancellationToken);

    private Task<bool> IsAuthorBlockedByViewerAsync(long? viewerUserId, long authorUserId, CancellationToken cancellationToken = default) =>
        MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            viewerUserId,
            authorUserId,
            (viewerId, authorIds, ct) => _socialClient.GetMutuallyBlockedUserIdsAsync(viewerId, authorIds, ct),
            cancellationToken);

    private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

    private long? TryGetViewerUserId() => _currentUser.TryGetViewerUserId();

    public async Task EnsurePostExistsAsync(long id, string notFoundMessage = "Post not found", int statusCode = StatusCodes.Status404NotFound)
    {
        if (!await _repo.ExistsPostByIdAsync(id)) throw new EntityNotFoundException(notFoundMessage, statusCode);
    }

    private async Task<Post> GetPostOrThrowAsync(
        long id,
        string notFoundMessage = "Post not found",
        int statusCode = StatusCodes.Status404NotFound) =>
        await _repo.GetPostByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage, statusCode);

    /// <summary>
    /// Ensures the viewer may see the post (exists, not blocked, board access).
    /// Missing or blocked posts surface as not-found for privacy.
    /// </summary>
    public async Task EnsureCanViewPostAsync(
        long postId,
        string notFoundMessage = "Post not found",
        int notFoundStatusCode = StatusCodes.Status404NotFound)
    {
        var post = await GetPostOrThrowAsync(postId, notFoundMessage, notFoundStatusCode);

        if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId))
            throw new EntityNotFoundException(notFoundMessage, notFoundStatusCode);

        if (post.GroupId is not null && post.GroupBoardId is not null)
            await _groupClient.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);
    }

    /// <summary>
    /// Ensures the caller may comment on the post (exists, not blocked, board write access).
    /// </summary>
    public async Task EnsureCanCommentOnPostAsync(long postId)
    {
        var post = await GetPostOrThrowAsync(postId, "Post not found", StatusCodes.Status400BadRequest);

        if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId))
            throw new EntityNotFoundException("Post not found", StatusCodes.Status400BadRequest);

        if (post.GroupId is not null && post.GroupBoardId is not null)
            await _groupClient.EnsureCanWritePostAsync(post.GroupId.Value, post.GroupBoardId.Value);
    }

    public async Task CreatePostAsync(PostCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);

        if (request.GroupId.HasValue || request.GroupBoardId.HasValue)
        {
            if (!request.GroupId.HasValue || !request.GroupBoardId.HasValue)
                throw new ArgumentException("GroupId and GroupBoardId must be provided together for group posts.");
            await _groupClient.EnsureCanWritePostAsync(request.GroupId.Value, request.GroupBoardId.Value);
        }

        ValidateOptionalLocation(request.Latitude, request.Longitude);

        var post = new Post(
            userId: userId,
            title: request.Title,
            content: request.Content,
            groupId: request.GroupId,
            groupBoardId: request.GroupBoardId);

        await _repo.CreatePostAsync(post);
        try
        {
            // Saga: persist → link media (idempotent) → upsert location (idempotent).
            await _mediaClient.LinkToPostAsync(post.Id, userId, request.MediaAssetIds);
            if (request.Latitude.HasValue)
            {
                await _locationClient.UpsertLocationForPostAsync(
                    post.Id,
                    userId,
                    request.Latitude.Value,
                    request.Longitude!.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post create side effects failed for post {PostId}; compensating", post.Id);
            await CompensateFailedPostSideEffectsAsync(post);
            throw;
        }
    }

    public async Task<List<PostGetResponseDto>?> GetAllPostsAsync()
    {
        var posts = await _repo.GetAllPostsAsync();
        if (posts.Count == 0) return null;

        posts = await FilterPostsByBlockAsync(TryGetViewerUserId(), posts);
        if (posts.Count == 0) return null;

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
        return await MapManyAsync(posts, nicknames);
    }

    public async Task<PostGetResponseDto?> GetPostByIdAsync(long id)
    {
        var post = await _repo.GetPostByIdAsync(id);
        if (post == null) return null;

        if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return null;

        if (post.GroupId is not null && post.GroupBoardId is not null)
            await _groupClient.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync([post.AuthorUserId]);
        return await MapToDtoAsync(post, nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User"));
    }

    public async Task<bool> TryCanViewPostAsync(long id)
    {
        var post = await _repo.GetPostByIdAsync(id);
        if (post is null) return false;

        if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return false;

        if (post.GroupId is not null && post.GroupBoardId is not null)
            return await _groupClient.TryCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);

        return true;
    }

    public async Task<HashSet<long>> GetViewablePostIdsAsync(IEnumerable<long> postIds, long? viewerUserId)
    {
        var posts = await _repo.GetPostsByIdsAsync(postIds);
        if (posts.Count == 0) return [];

        var blockedAuthorIds = viewerUserId is null
            ? []
            : await _socialClient.GetMutuallyBlockedUserIdsAsync(
                viewerUserId.Value,
                [.. posts.Select(p => p.AuthorUserId).Distinct()]);

        var boardKeys = posts
            .Where(p => p.GroupId is not null && p.GroupBoardId is not null)
            .Select(p => (p.GroupId!.Value, p.GroupBoardId!.Value))
            .Distinct()
            .ToList();
        var viewableBoards = boardKeys.Count == 0
            ? []
            : await _groupClient.ResolveViewableBoardKeysAsync(boardKeys);

        HashSet<long> viewable = [];
        foreach (var post in posts)
        {
            if (viewerUserId is not null && blockedAuthorIds.Contains(post.AuthorUserId)) continue;

            if (post.GroupId is not null && post.GroupBoardId is not null
                && !viewableBoards.Contains((post.GroupId.Value, post.GroupBoardId.Value)))
            {
                continue;
            }

            viewable.Add(post.Id);
        }

        return viewable;
    }

    public async Task CreateGroupBoardPostAsync(long groupId, long boardId, GroupBoardPostCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        await _groupClient.EnsureCanWritePostAsync(groupId, boardId);

        ValidateOptionalLocation(request.Latitude, request.Longitude);

        var post = new Post(userId, request.Title, request.Content, groupId, boardId);
        await _repo.CreatePostAsync(post);
        try
        {
            // Saga: persist → link media (idempotent) → upsert location (idempotent).
            await _mediaClient.LinkToPostAsync(post.Id, userId, request.MediaAssetIds);
            if (request.Latitude.HasValue)
            {
                await _locationClient.UpsertLocationForPostAsync(
                    post.Id,
                    userId,
                    request.Latitude.Value,
                    request.Longitude!.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Group board post create side effects failed for post {PostId}; compensating",
                post.Id);
            await CompensateFailedPostSideEffectsAsync(post);
            throw;
        }
    }

    public async Task<List<PostGetResponseDto>?> GetGroupBoardPostsAsync(long groupId, long boardId)
    {
        await _groupClient.EnsureCanViewBoardAsync(groupId, boardId);
        var posts = await _repo.GetPostsByGroupBoardAsync(groupId, boardId);
        if (posts.Count == 0) return null;

        posts = await FilterPostsByBlockAsync(TryGetViewerUserId(), posts);
        if (posts.Count == 0) return null;

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
        return await MapManyAsync(posts, nicknames);
    }

    public async Task<PostGetResponseDto?> GetGroupBoardPostByIdAsync(long groupId, long boardId, long postId)
    {
        await _groupClient.EnsureCanViewBoardAsync(groupId, boardId);
        var post = await _repo.GetGroupBoardPostAsync(groupId, boardId, postId);
        if (post == null) return null;

        if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return null;

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync([post.AuthorUserId]);
        return await MapToDtoAsync(post, nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User"));
    }

    public async Task<IReadOnlyDictionary<long, (long GroupId, long GroupBoardId)>> GetGroupBoardContextsByPostIdsAsync(
        IEnumerable<long> postIds)
    {
        var posts = await _repo.GetPostsByIdsAsync(postIds);
        return posts
            .Where(p => p.GroupId is not null && p.GroupBoardId is not null)
            .ToDictionary(p => p.Id, p => (p.GroupId!.Value, p.GroupBoardId!.Value));
    }

    public async Task<List<PostGetResponseDto>?> GetPostsByUserNicknameAsync(string nickname)
    {
        var viewerUserId = TryGetViewerUserId();
        var userId = await _userClient.GetUserIdByNicknameAsync(nickname);
        if (userId is null) return null;
        if (await IsAuthorBlockedByViewerAsync(viewerUserId, userId.Value)) return null;

        var posts = await _repo.GetPostsByUserIdAsync(userId.Value);

        var boardKeys = posts
            .Where(p => p.GroupId is not null && p.GroupBoardId is not null)
            .Select(p => (p.GroupId!.Value, p.GroupBoardId!.Value))
            .Distinct()
            .ToList();
        var viewableBoards = boardKeys.Count == 0
            ? []
            : await _groupClient.ResolveViewableBoardKeysAsync(boardKeys);

        posts = [.. posts.Where(p =>
            p.GroupId is null ||
            viewableBoards.Contains((p.GroupId.Value, p.GroupBoardId!.Value)))];
        if (posts.Count == 0) return null;

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync([userId.Value]);
        return await MapManyAsync(posts, nicknames);
    }

    private async Task<List<PostGetResponseDto>> MapManyAsync(
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<long, string> nicknames)
    {
        var mediaByPostId = await _mediaClient.GetMediaByPostIdsAsync([.. posts.Select(p => p.Id)]);
        var locationsByPostId = await _locationClient.GetLocationsByPostIdsAsync([.. posts.Select(p => p.Id)]);

        return [.. posts.Select(post => MapToDto(
            post,
            nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User"),
            mediaByPostId.GetValueOrDefault(post.Id) ?? [],
            locationsByPostId.GetValueOrDefault(post.Id)))];
    }

    private async Task<PostGetResponseDto> MapToDtoAsync(Post post, string authorNickname)
    {
        var media = await _mediaClient.GetMediaForPostAsync(post.Id);
        var locations = await _locationClient.GetLocationsByPostIdsAsync([post.Id]);
        return MapToDto(post, authorNickname, media, locations.GetValueOrDefault(post.Id));
    }

    private static PostGetResponseDto MapToDto(
        Post post,
        string authorNickname,
        IReadOnlyList<MediaAssetGetResponseDto> media,
        PostLocationGetResponseDto? location) =>
        new(
            post.Id,
            post.Title,
            post.Content,
            post.CreatedAt,
            post.UpdatedAt,
            post.AuthorUserId,
            authorNickname,
            media,
            location);

    public async Task<PostPatchResponseDto> UpdatePostAsync(PostPatchRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        var post = await GetPostOrThrowAsync(request.Id);
        if (post.GroupId is not null && post.GroupBoardId is not null)
            await _groupClient.EnsureCanWritePostAsync(post.GroupId.Value, post.GroupBoardId.Value);
        if (post.AuthorUserId != userId)
            throw new AccessForbiddenException("Unauthorized access");
        ValidateLocationPatch(request);

        post.Update(request.Title, request.Content);
        await _repo.UpdatePostAsync(post);
        await _mediaClient.PatchPostMediaAsync(
            post.Id,
            userId,
            request.AddMediaAssetIds,
            request.RemoveMediaAssetIds);
        if (request.ClearLocation)
            await _locationClient.ClearLocationForPostAsync(post.Id, userId);
        else if (request.Latitude.HasValue)
        {
            await _locationClient.UpsertLocationForPostAsync(
                post.Id,
                userId,
                request.Latitude.Value,
                request.Longitude!.Value);
        }

        return new PostPatchResponseDto(
            Title: post.Title,
            Content: post.Content,
            UpdatedAt: post.UpdatedAt);
    }

    public Task DetachAuthorFromDeletedUserAsync(long userId) =>
        _repo.DetachAuthorFromPostsAsync(userId);

    public Task<bool> PostExistsAsync(long postId) =>
        _repo.ExistsPostByIdAsync(postId);

    public async Task<(long GroupId, long GroupBoardId)?> TryGetGroupBoardContextAsync(long postId)
    {
        var post = await _repo.GetPostByIdAsync(postId);
        if (post is null) return null;
        if (post.GroupId is null || post.GroupBoardId is null) return null;
        return (post.GroupId.Value, post.GroupBoardId.Value);
    }

    public Task EnsureCanViewPostMediaAsync(long postId) =>
        EnsureCanViewPostAsync(postId);

    public async Task EnsureCallerOwnsPostAsync(long postId)
    {
        var callerId = GetUserIdFromLogin();
        var post = await _repo.GetPostByIdAsync(postId)
            ?? throw new EntityNotFoundException("Post not found", StatusCodes.Status400BadRequest);
        if (post.AuthorUserId != callerId)
            throw new AccessForbiddenException("Unauthorized access");
    }

    public async Task DeleteAllByGroupAsync(long groupId)
    {
        var postIds = await _repo.GetPostIdsByGroupAsync(groupId);
        if (postIds.Count > 0)
        {
            await _commentService.Value.DeleteAllForPostIdsAsync(postIds);
            await _mediaClient.DeleteBlobStorageForPostsAsync(postIds);
            foreach (var postId in postIds)
                await _locationClient.ClearLocationForPostOnDeleteAsync(postId);
        }

        await _repo.DeleteAllByGroupAsync(groupId);
    }

    public async Task DeletePostAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        var post = await GetPostOrThrowAsync(id);
        if (post.GroupId is not null && post.GroupBoardId is not null)
            await _groupClient.EnsureCanWritePostAsync(post.GroupId.Value, post.GroupBoardId.Value);
        if (post.AuthorUserId != userId)
            throw new AccessForbiddenException("Unauthorized access");

        // Remote-first: dispose side effects, then local delete (idempotent remotes).
        try
        {
            await _mediaClient.DeleteBlobStorageForPostAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote cleanup failed before deleting post {PostId} (media)", id);
            throw;
        }

        try
        {
            await _locationClient.ClearLocationForPostOnDeleteAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote cleanup failed before deleting post {PostId} (location)", id);
            throw;
        }

        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _commentService.Value.DetachCommentsFromDeletedPostAsync(id);
            await _repo.DeletePostAsync(post);
        });
    }

    private async Task CompensateFailedPostSideEffectsAsync(Post post)
    {
        try
        {
            await _mediaClient.UnlinkFromPostAsync(post.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation: media unlink failed for post {PostId}", post.Id);
        }

        try
        {
            await _locationClient.ClearLocationForPostOnDeleteAsync(post.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation: location clear failed for post {PostId}", post.Id);
        }

        try
        {
            await _repo.DeletePostAsync(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation: failed to delete post {PostId}", post.Id);
        }
    }

    private static void ValidateOptionalLocation(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
            throw new ArgumentException("Latitude and longitude must be provided together.");

        if (latitude.HasValue) ValidateLocationBounds(latitude.Value, longitude!.Value);
    }

    private static void ValidateLocationPatch(PostPatchRequestDto request)
    {
        if (request.ClearLocation && (request.Latitude.HasValue || request.Longitude.HasValue))
            throw new ArgumentException("ClearLocation cannot be combined with latitude or longitude.");

        ValidateOptionalLocation(request.Latitude, request.Longitude);
    }

    private static void ValidateLocationBounds(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90) throw new ArgumentException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new ArgumentException("Longitude must be between -180 and 180.");
    }
}
