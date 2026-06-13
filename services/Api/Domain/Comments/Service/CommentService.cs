using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Dto;
using Api.Domain.Media.Dto;
using Api.Domain.Comments.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Domain.Groups.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Comments.Service
{
    [Service]
    public class CommentService(
        ICommentRepository repo,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        PostService postService,
        GroupBoardAccessService groupBoardAccess,
        UserService userService,
        Lazy<MediaService> mediaService)
    {
        private readonly ICommentRepository _repo = repo;
        private readonly AppDbContext _db = db;
        private readonly Lazy<MediaService> _mediaService = mediaService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly PostService _postService = postService;
        private readonly GroupBoardAccessService _groupBoardAccess = groupBoardAccess;
        private readonly UserService _userService = userService;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        private async Task<Comment> GetCommentOrThrowAsync(long id, string notFoundMessage = "Comment not found", int statusCode = StatusCodes.Status404NotFound) =>
            await _repo.GetCommentByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage, statusCode);

        private async Task EnsureGroupBoardViewAccessForPostAsync(long postId)
        {
            var groupBoard = await _postService.TryGetGroupBoardContextAsync(postId);
            if (groupBoard is not null) await _groupBoardAccess.EnsureCanViewBoardAsync(groupBoard.Value.GroupId, groupBoard.Value.GroupBoardId);
        }

        private async Task EnsureGroupBoardWriteAccessForPostAsync(long postId)
        {
            var groupBoard = await _postService.TryGetGroupBoardContextAsync(postId);
            if (groupBoard is not null) await _groupBoardAccess.EnsureCanWritePostAsync(groupBoard.Value.GroupId, groupBoard.Value.GroupBoardId);
        }

        public async Task CreateCommentAsync(CommentCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _postService.EnsurePostExistsAsync(request.PostId, statusCode: StatusCodes.Status400BadRequest);

            await EnsureGroupBoardWriteAccessForPostAsync(request.PostId);

            if (request.ParentId.HasValue)
            {
                var parentComment = await GetCommentOrThrowAsync(request.ParentId.Value, "Parent comment not found", StatusCodes.Status400BadRequest);
                if (parentComment.LogicalPostId != request.PostId) throw new ArgumentException("Parent comment must belong to the same post");
            }

            var content = request.Content.Trim();
            if (content.Length == 0 && request.MediaAssetId is null)
                throw new ArgumentException("Comment content cannot be empty.");

            var comment = new Comment(
                content: content,
                userId: userId,
                postId: request.PostId,
                parentId: request.ParentId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreateCommentAsync(comment);
                await _mediaService.Value.LinkToCommentAsync(comment.Id, userId, request.MediaAssetId);
            });
        }

        public async Task<CommentGetResponseDto?> GetCommentByIdAsync(long id)
        {
            var comment = await _repo.GetCommentByIdAsync(id);
            if (comment == null) return null;
            if (comment.PostId is not null) await EnsureGroupBoardViewAccessForPostAsync(comment.PostId.Value);
            var nicknames = await _userService.GetNicknamesByUserIdsAsync([comment.AuthorUserId]);
            var mediaByCommentId = await _mediaService.Value.GetMediaByCommentIdsAsync([comment.Id]);
            return MapToDto(
                comment,
                nicknames.GetValueOrDefault(comment.AuthorUserId, "Deleted User"),
                mediaByCommentId.GetValueOrDefault(comment.Id));
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByPostIdAsync(long postId)
        {
            await _postService.EnsurePostExistsAsync(postId);
            await EnsureGroupBoardViewAccessForPostAsync(postId);
            var comments = await _repo.GetCommentsByPostIdAsync(postId);
            if (comments.Count == 0) return null;
            return await BuildCommentTreeAsync(comments);
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByUserIdAsync(long userId)
        {
            var comments = await _repo.GetCommentsByUserIdAsync(userId);
            if (comments.Count == 0)
            {
                await _userService.EnsureUserExistsAsync(userId);
                return null;
            }

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(comments.Select(c => c.AuthorUserId).Distinct());
            var mediaByCommentId = await _mediaService.Value.GetMediaByCommentIdsAsync([.. comments.Select(c => c.Id)]);
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
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(comments.Select(c => c.AuthorUserId).Distinct());
            var mediaByCommentId = await _mediaService.Value.GetMediaByCommentIdsAsync([.. comments.Select(c => c.Id)]);
            var byId = comments.ToDictionary(
                c => c.Id,
                c => MapToDto(
                    c,
                    nicknames.GetValueOrDefault(c.AuthorUserId, "Deleted User"),
                    mediaByCommentId.GetValueOrDefault(c.Id)));
            List<CommentGetResponseDto> roots = [];

            foreach (var comment in comments.OrderBy(c => c.CreatedAt))
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
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var comment = await GetCommentOrThrowAsync(request.Id);
            if (comment.PostId is null && comment.DeletedPostId is not null) throw new ArgumentException("Post is not reachable. Comments are readonly.");
            if (comment.PostId is not null) await EnsureGroupBoardViewAccessForPostAsync(comment.PostId.Value);
            if (comment.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            comment.UpdateContent(request.Content);
            await _repo.UpdateCommentAsync(comment);
            return new CommentPatchResponseDto
            {
                Content = comment.Content,
                PostId = comment.PostId,
                DeletedPostId = comment.DeletedPostId,
                UpdatedAt = comment.UpdatedAt
            };
        }

        public Task DetachCommentsFromDeletedPostAsync(long postId) =>
            _repo.DetachPostFromCommentsAsync(postId);

        public async Task DeleteAllForPostIdsAsync(IReadOnlyCollection<long> postIds)
        {
            if (postIds.Count == 0) return;

            var commentIds = await _repo.GetCommentIdsByPostIdsAsync(postIds);
            if (commentIds.Count > 0)
                await _mediaService.Value.DeleteBlobStorageForCommentsAsync(commentIds);

            await _repo.DeleteAllForPostIdsAsync(postIds);
        }

        public Task DetachAuthorFromDeletedUserAsync(long userId) =>
            _repo.DetachAuthorFromCommentsAsync(userId);

        public async Task DeleteCommentAsync(long id)
        {
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var comment = await GetCommentOrThrowAsync(id);
            if (comment.PostId is not null) await EnsureGroupBoardViewAccessForPostAsync(comment.PostId.Value);
            if (comment.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.DetachParentFromRepliesAsync(id);
                await _mediaService.Value.DeleteBlobStorageForCommentAsync(id);
                await _repo.DeleteCommentAsync(comment);
            });
        }
    }
}
