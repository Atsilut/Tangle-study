using Api.Client;
using Api.Domain.Comments.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Posts.Service
{
    [Service]
    public class PostService(
        IPostRepository repo,
        AppDbContext db,
        Lazy<CommentService> commentService,
        IMediaClient mediaClient,
        ILocationClient locationClient,
        IHttpContextAccessor httpContextAccessor,
        UserService userService,
        UserBlockService userBlockService,
        GroupBoardAccessService groupBoardAccess)
    {
        private readonly IPostRepository _repo = repo;
        private readonly AppDbContext _db = db;
        private readonly Lazy<CommentService> _commentService = commentService;
        private readonly IMediaClient _mediaClient = mediaClient;
        private readonly ILocationClient _locationClient = locationClient;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly UserService _userService = userService;
        private readonly UserBlockService _userBlockService = userBlockService;
        private readonly GroupBoardAccessService _groupBoardAccess = groupBoardAccess;

        private long? TryGetViewerUserId()
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) ? id : null;
        }

        private async Task<List<Post>> FilterPostsByBlockAsync(long? viewerUserId, List<Post> posts)
        {
            if (viewerUserId is null || posts.Count == 0) return posts;

            var blockedAuthorIds = await _userBlockService.GetMutuallyBlockedUserIdsAsync(
                viewerUserId.Value,
                posts.Select(p => p.AuthorUserId).Distinct().ToList());
            if (blockedAuthorIds.Count == 0) return posts;

            return [.. posts.Where(p => !blockedAuthorIds.Contains(p.AuthorUserId))];
        }

        private async Task<bool> IsAuthorBlockedByViewerAsync(long? viewerUserId, long authorUserId)
        {
            if (viewerUserId is null || viewerUserId.Value == authorUserId) return false;

            var blockedIds = await _userBlockService.GetMutuallyBlockedUserIdsAsync(viewerUserId.Value, [authorUserId]);
            return blockedIds.Contains(authorUserId);
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task EnsurePostExistsAsync(long id, string notFoundMessage = "Post not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsPostByIdAsync(id)) throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        private async Task<Post> GetPostOrThrowAsync(long id, string notFoundMessage = "Post not found") =>
            await _repo.GetPostByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage);

        public async Task CreatePostAsync(PostCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);

            if (request.GroupId.HasValue || request.GroupBoardId.HasValue)
            {
                if (!request.GroupId.HasValue || !request.GroupBoardId.HasValue) throw new ArgumentException("GroupId and GroupBoardId must be provided together for group posts.");
                await _groupBoardAccess.EnsureCanWritePostAsync(request.GroupId.Value, request.GroupBoardId.Value);
            }

            ValidateOptionalLocation(request.Latitude, request.Longitude);

            var post = new Post(
                userId: userId,
                title: request.Title,
                content: request.Content,
                groupId: request.GroupId,
                groupBoardId: request.GroupBoardId
            );
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreatePostAsync(post);
                await _mediaClient.LinkToPostAsync(post.Id, userId, request.MediaAssetIds);
                if (request.Latitude.HasValue)
                {
                    await _locationClient.UpsertLocationForPostAsync(
                        post.Id,
                        userId,
                        request.Latitude.Value,
                        request.Longitude!.Value);
                }
            });
        }

        public async Task<List<PostGetResponseDto>?> GetAllPostsAsync()
        {
            var posts = await _repo.GetAllPostsAsync();
            if (posts.Count == 0) return null;

            posts = await FilterPostsByBlockAsync(TryGetViewerUserId(), posts);
            if (posts.Count == 0) return null;

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
            return await MapManyAsync(posts, nicknames);
        }

        public async Task<PostGetResponseDto?> GetPostByIdAsync(long id)
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null) return null;

            if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return null;

            if (post.GroupId is not null && post.GroupBoardId is not null) await _groupBoardAccess.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);

            var user = await _userService.GetUserByIdAsync(post.AuthorUserId);
            return await MapToDtoAsync(post, user?.Nickname ?? "Deleted User");
        }

        /// <summary>Non-throwing visibility check for callers that filter collections (e.g. map pins).</summary>
        public async Task<bool> TryCanViewPostAsync(long id)
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post is null) return false;

            if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return false;

            if (post.GroupId is not null && post.GroupBoardId is not null)
                return await _groupBoardAccess.TryCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);

            return true;
        }

        public async Task<HashSet<long>> GetViewablePostIdsAsync(IEnumerable<long> postIds, long? viewerUserId)
        {
            var posts = await _repo.GetPostsByIdsAsync(postIds);
            if (posts.Count == 0) return [];

            var blockedAuthorIds = viewerUserId is null
                ? []
                : await _userBlockService.GetMutuallyBlockedUserIdsAsync(
                    viewerUserId.Value,
                    posts.Select(p => p.AuthorUserId).Distinct().ToList());

            HashSet<long> viewable = [];
            foreach (var post in posts)
            {
                if (viewerUserId is not null && blockedAuthorIds.Contains(post.AuthorUserId)) continue;

                if (post.GroupId is not null && post.GroupBoardId is not null)
                {
                    if (!await _groupBoardAccess.TryCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value))
                        continue;
                }

                viewable.Add(post.Id);
            }

            return viewable;
        }

        public async Task CreateGroupBoardPostAsync(long groupId, long boardId, GroupBoardPostCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);
            await _groupBoardAccess.EnsureCanWritePostAsync(groupId, boardId);

            ValidateOptionalLocation(request.Latitude, request.Longitude);

            var post = new Post(userId, request.Title, request.Content, groupId, boardId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreatePostAsync(post);
                await _mediaClient.LinkToPostAsync(post.Id, userId, request.MediaAssetIds);
                if (request.Latitude.HasValue)
                {
                    await _locationClient.UpsertLocationForPostAsync(
                        post.Id,
                        userId,
                        request.Latitude.Value,
                        request.Longitude!.Value);
                }
            });
        }

        public async Task<List<PostGetResponseDto>?> GetGroupBoardPostsAsync(long groupId, long boardId)
        {
            await _groupBoardAccess.EnsureCanViewBoardAsync(groupId, boardId);
            var posts = await _repo.GetPostsByGroupBoardAsync(groupId, boardId);
            if (posts.Count == 0) return null;

            posts = await FilterPostsByBlockAsync(TryGetViewerUserId(), posts);
            if (posts.Count == 0) return null;

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
            return await MapManyAsync(posts, nicknames);
        }

        public async Task<PostGetResponseDto?> GetGroupBoardPostByIdAsync(long groupId, long boardId, long postId)
        {
            await _groupBoardAccess.EnsureCanViewBoardAsync(groupId, boardId);
            var post = await _repo.GetGroupBoardPostAsync(groupId, boardId, postId);
            if (post == null) return null;

            if (await IsAuthorBlockedByViewerAsync(TryGetViewerUserId(), post.AuthorUserId)) return null;

            var user = await _userService.GetUserByIdAsync(post.AuthorUserId);
            return await MapToDtoAsync(post, user?.Nickname ?? "Deleted User");
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
            var user = await _userService.GetUserByNicknameAsync(nickname);
            if (user == null) return null;
            if (await IsAuthorBlockedByViewerAsync(viewerUserId, user.Id)) return null;
            var posts = await _repo.GetPostsByUserIdAsync(user.Id);

            var boardKeys = posts
                .Where(p => p.GroupId is not null && p.GroupBoardId is not null)
                .Select(p => (p.GroupId!.Value, p.GroupBoardId!.Value))
                .Distinct()
                .ToList();
            var viewableBoards = boardKeys.Count == 0
                ? []
                : await _groupBoardAccess.ResolveViewableBoardKeysAsync(boardKeys);

            posts = [.. posts.Where(p =>
                p.GroupId is null ||
                viewableBoards.Contains((p.GroupId.Value, p.GroupBoardId!.Value)))];
            if (posts.Count == 0) return null;

            return await MapManyAsync(posts, new Dictionary<long, string> { [user.Id] = user.Nickname });
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
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var post = await GetPostOrThrowAsync(request.Id);
            if (post.GroupId is not null && post.GroupBoardId is not null) await _groupBoardAccess.EnsureCanWritePostAsync(post.GroupId.Value, post.GroupBoardId.Value);
            if (post.AuthorUserId != user.Id) throw new UnauthorizedAccessException();
            ValidateLocationPatch(request);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                post.Update(request.Title, request.Content);
                await _repo.UpdatePostAsync(post);
                await _mediaClient.PatchPostMediaAsync(
                    post.Id,
                    user.Id,
                    request.AddMediaAssetIds,
                    request.RemoveMediaAssetIds);
                if (request.ClearLocation)
                    await _locationClient.ClearLocationForPostAsync(post.Id, user.Id);
                else if (request.Latitude.HasValue)
                {
                    await _locationClient.UpsertLocationForPostAsync(
                        post.Id,
                        user.Id,
                        request.Latitude.Value,
                        request.Longitude!.Value);
                }
            });
            return new PostPatchResponseDto(
                Title: post.Title,
                Content: post.Content,
                UpdatedAt: post.UpdatedAt
            );
        }

        public Task DetachAuthorFromDeletedUserAsync(long userId) =>
            _repo.DetachAuthorFromPostsAsync(userId);

        public async Task<(long GroupId, long GroupBoardId)?> TryGetGroupBoardContextAsync(long postId)
        {
            var post = await _repo.GetPostByIdAsync(postId);
            if (post is null) return null;
            if (post.GroupId is null || post.GroupBoardId is null) return null;
            return (post.GroupId.Value, post.GroupBoardId.Value);
        }

        public async Task EnsureCanViewPostMediaAsync(long postId)
        {
            var ctx = await TryGetGroupBoardContextAsync(postId);
            if (ctx is not null)
                await _groupBoardAccess.EnsureCanViewBoardAsync(ctx.Value.GroupId, ctx.Value.GroupBoardId);
        }

        public async Task EnsureCallerOwnsPostAsync(long postId)
        {
            var callerId = GetUserIdFromLogin();
            var post = await _repo.GetPostByIdAsync(postId)
                ?? throw new EntityNotFoundException("Post not found", StatusCodes.Status400BadRequest);
            if (post.AuthorUserId != callerId) throw new UnauthorizedAccessException("Unauthorized access");
        }

        public async Task DeleteAllByGroupAsync(long groupId)
        {
            var postIds = await _repo.GetPostIdsByGroupAsync(groupId);
            if (postIds.Count > 0)
            {
                await _commentService.Value.DeleteAllForPostIdsAsync(postIds);
                await _mediaClient.DeleteBlobStorageForPostsAsync(postIds);
            }

            await _repo.DeleteAllByGroupAsync(groupId);
        }

        public async Task DeletePostAsync(long id)
        {
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var post = await GetPostOrThrowAsync(id);
            if (post.GroupId is not null && post.GroupBoardId is not null) await _groupBoardAccess.EnsureCanWritePostAsync(post.GroupId.Value, post.GroupBoardId.Value);
            if (post.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _commentService.Value.DetachCommentsFromDeletedPostAsync(id);
                await _mediaClient.DeleteBlobStorageForPostAsync(id);
                await _locationClient.ClearLocationForPostOnDeleteAsync(id);
                await _repo.DeletePostAsync(post);
            });
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
}
