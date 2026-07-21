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
public class CommentService(
    ICommentRepository repo,
    CommunityDbContext db,
    CurrentUserAccessor currentUser,
    PostService postService,
    IUserClient userClient,
    ISocialClient socialClient,
    IGroupClient groupClient,
    IMediaClient mediaClient,
    ILogger<CommentService> logger)
{
    private readonly ICommentRepository _repo = repo;
    private readonly CommunityDbContext _db = db;
    private readonly IMediaClient _mediaClient = mediaClient;
    private readonly CurrentUserAccessor _currentUser = currentUser;
    private readonly PostService _postService = postService;
    private readonly IUserClient _userClient = userClient;
    private readonly ISocialClient _socialClient = socialClient;
    private readonly IGroupClient _groupClient = groupClient;
    private readonly ILogger<CommentService> _logger = logger;

    private Task<bool> IsAuthorBlockedByViewerAsync(long authorUserId, CancellationToken cancellationToken = default) =>
        MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            _currentUser.TryGetViewerUserId(),
            authorUserId,
            (viewerId, authorIds, ct) => _socialClient.GetMutuallyBlockedUserIdsAsync(viewerId, authorIds, ct),
            cancellationToken);

    private Task<List<Comment>> FilterCommentsByBlockAsync(IReadOnlyList<Comment> comments, CancellationToken cancellationToken = default) =>
        MutualBlockFilter.FilterByMutualBlockAsync(
            _currentUser.TryGetViewerUserId(),
            comments,
            c => c.AuthorUserId,
            (viewerId, authorIds, ct) => _socialClient.GetMutuallyBlockedUserIdsAsync(viewerId, authorIds, ct),
            cancellationToken);

    private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

    private async Task<Comment> GetCommentOrThrowAsync(
        long id,
        string notFoundMessage = "Comment not found",
        int statusCode = StatusCodes.Status404NotFound) =>
        await _repo.GetCommentByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage, statusCode);

    private async Task EnsureGroupBoardWriteAccessForPostAsync(long postId)
    {
        var groupBoard = await _postService.TryGetGroupBoardContextAsync(postId);
        if (groupBoard is not null)
            await _groupClient.EnsureCanWritePostAsync(groupBoard.Value.GroupId, groupBoard.Value.GroupBoardId);
    }

    public async Task CreateCommentAsync(CommentCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        await _postService.EnsureCanCommentOnPostAsync(request.PostId);

        if (request.ParentId.HasValue)
        {
            var parentComment = await GetCommentOrThrowAsync(
                request.ParentId.Value,
                "Parent comment not found",
                StatusCodes.Status400BadRequest);
            if (parentComment.LogicalPostId != request.PostId)
                throw new ArgumentException("Parent comment must belong to the same post");
        }

        var content = request.Content.Trim();
        if (content.Length == 0 && request.MediaAssetId is null)
            throw new ArgumentException("Comment content cannot be empty.");

        var comment = new Comment(
            content: content,
            userId: userId,
            postId: request.PostId,
            parentId: request.ParentId);
        await _repo.CreateCommentAsync(comment);
        try
        {
            // Saga: persist → link media (idempotent).
            await _mediaClient.LinkToCommentAsync(comment.Id, userId, request.MediaAssetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Comment create side effects failed for comment {CommentId}; compensating",
                comment.Id);
            await CompensateFailedCommentSideEffectsAsync(comment);
            throw;
        }
    }

    private async Task CompensateFailedCommentSideEffectsAsync(Comment comment)
    {
        try
        {
            await _mediaClient.UnlinkFromCommentAsync(comment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation: media unlink failed for comment {CommentId}", comment.Id);
        }

        try
        {
            await _repo.DeleteCommentAsync(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation: failed to delete comment {CommentId}", comment.Id);
        }
    }

    public async Task<CommentGetResponseDto?> GetCommentByIdAsync(long id)
    {
        var comment = await _repo.GetCommentByIdAsync(id);
        if (comment == null) return null;
        if (await IsAuthorBlockedByViewerAsync(comment.AuthorUserId)) return null;
        if (comment.PostId is not null)
            await _postService.EnsureCanViewPostAsync(comment.PostId.Value);
        var nicknames = await _userClient.GetNicknamesByUserIdsAsync([comment.AuthorUserId]);
        var mediaByCommentId = await _mediaClient.GetMediaByCommentIdsAsync([comment.Id]);
        return MapToDto(
            comment,
            nicknames.GetValueOrDefault(comment.AuthorUserId, "Deleted User"),
            mediaByCommentId.GetValueOrDefault(comment.Id));
    }

    public async Task<List<CommentGetResponseDto>?> GetCommentsByPostIdAsync(long postId)
    {
        await _postService.EnsureCanViewPostAsync(postId);
        var comments = await _repo.GetCommentsByPostIdAsync(postId);
        if (comments.Count == 0) return null;
        comments = await FilterCommentsByBlockAsync(comments);
        if (comments.Count == 0) return null;
        return await BuildCommentTreeAsync(comments);
    }

    public async Task EnsureCanViewCommentMediaAsync(long commentId)
    {
        var comment = await _repo.GetCommentByIdAsync(commentId)
            ?? throw new EntityNotFoundException("Comment not found");
        if (await IsAuthorBlockedByViewerAsync(comment.AuthorUserId))
            throw new EntityNotFoundException("Comment not found");
        if (comment.PostId is not null)
            await _postService.EnsureCanViewPostAsync(comment.PostId.Value);
    }

    public async Task<List<CommentGetResponseDto>?> GetCommentsByUserIdAsync(long userId)
    {
        if (await IsAuthorBlockedByViewerAsync(userId)) return null;

        var comments = await _repo.GetCommentsByUserIdAsync(userId);
        if (comments.Count == 0)
        {
            await _userClient.EnsureUserExistsAsync(userId);
            return null;
        }

        var postIds = comments.Where(c => c.PostId is not null).Select(c => c.PostId!.Value).Distinct().ToList();
        var viewablePostIds = postIds.Count == 0
            ? []
            : await _postService.GetViewablePostIdsAsync(postIds, _currentUser.TryGetViewerUserId());

        comments = [.. comments.Where(c => c.PostId is null || viewablePostIds.Contains(c.PostId.Value))];
        if (comments.Count == 0) return null;

        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(comments.Select(c => c.AuthorUserId).Distinct());
        var mediaByCommentId = await _mediaClient.GetMediaByCommentIdsAsync([.. comments.Select(c => c.Id)]);
        return [.. comments.Select(c => MapToDto(
            c,
            nicknames.GetValueOrDefault(c.AuthorUserId, "Deleted User"),
            mediaByCommentId.GetValueOrDefault(c.Id)))];
    }

    private static CommentGetResponseDto MapToDto(
        Comment comment,
        string authorNickname,
        MediaAssetGetResponseDto? media) => new()
    {
        Id = comment.Id,
        Content = comment.Content,
        AuthorId = comment.AuthorUserId,
        AuthorNickname = authorNickname,
        UserId = comment.UserId,
        DeletedUserId = comment.DeletedUserId,
        PostId = comment.PostId,
        DeletedPostId = comment.DeletedPostId,
        ParentId = comment.ParentId,
        DeletedParentId = comment.DeletedParentId,
        CreatedAt = comment.CreatedAt,
        UpdatedAt = comment.UpdatedAt,
        Media = media,
    };

    private async Task<List<CommentGetResponseDto>> BuildCommentTreeAsync(IReadOnlyList<Comment> comments)
    {
        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(comments.Select(c => c.AuthorUserId).Distinct());
        var mediaByCommentId = await _mediaClient.GetMediaByCommentIdsAsync([.. comments.Select(c => c.Id)]);
        var byId = comments.ToDictionary(
            c => c.Id,
            c => MapToDto(
                c,
                nicknames.GetValueOrDefault(c.AuthorUserId, "Deleted User"),
                mediaByCommentId.GetValueOrDefault(c.Id)));
        List<CommentGetResponseDto> roots = [];

        foreach (var comment in comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id))
        {
            var dto = byId[comment.Id];
            if (comment.ParentId is null)
            {
                roots.Add(dto);
                continue;
            }

            if (byId.TryGetValue(comment.ParentId.Value, out var parent)) parent.Replies.Add(dto);
        }

        return roots;
    }

    public async Task<CommentPatchResponseDto> UpdateCommentAsync(CommentPatchRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        var comment = await GetCommentOrThrowAsync(request.Id);
        if (comment.PostId is null && comment.DeletedPostId is not null)
            throw new ArgumentException("Post is not reachable. Comments are readonly.");
        if (comment.PostId is not null) await EnsureGroupBoardWriteAccessForPostAsync(comment.PostId.Value);
        if (comment.AuthorUserId != userId)
            throw new AccessForbiddenException("Unauthorized access");
        comment.UpdateContent(request.Content);
        await _repo.UpdateCommentAsync(comment);
        return new CommentPatchResponseDto
        {
            Content = comment.Content,
            PostId = comment.PostId,
            DeletedPostId = comment.DeletedPostId,
            UpdatedAt = comment.UpdatedAt,
        };
    }

    public Task DetachCommentsFromDeletedPostAsync(long postId) =>
        _repo.DetachPostFromCommentsAsync(postId);

    public async Task DeleteAllForPostIdsAsync(IReadOnlyCollection<long> postIds)
    {
        if (postIds.Count == 0) return;

        var commentIds = await _repo.GetCommentIdsByPostIdsAsync(postIds);
        if (commentIds.Count > 0)
            await _mediaClient.DeleteBlobStorageForCommentsAsync(commentIds);

        await _repo.DeleteAllForPostIdsAsync(postIds);
    }

    public Task DetachAuthorFromDeletedUserAsync(long userId) =>
        _repo.DetachAuthorFromCommentsAsync(userId);

    public async Task<bool> CommentExistsAsync(long commentId) =>
        await _repo.GetCommentByIdAsync(commentId) is not null;

    public async Task DeleteCommentAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);
        var comment = await GetCommentOrThrowAsync(id);
        if (comment.PostId is not null) await EnsureGroupBoardWriteAccessForPostAsync(comment.PostId.Value);
        if (comment.AuthorUserId != userId)
            throw new AccessForbiddenException("Unauthorized access");

        // Remote-first: dispose media, then local delete.
        try
        {
            await _mediaClient.DeleteBlobStorageForCommentAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote cleanup failed before deleting comment {CommentId} (media)", id);
            throw;
        }

        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _repo.DetachParentFromRepliesAsync(id);
            await _repo.DeleteCommentAsync(comment);
        });
    }
}
