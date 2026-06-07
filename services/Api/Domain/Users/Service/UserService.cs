using Api.Domain.Comments.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Db;
using Api.Global.Events;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Api.Domain.Users.Service
{
    [Service]
    public class UserService(
        IUserRepository repo,
        AppDbContext db,
        Lazy<PostService> postService,
        Lazy<CommentService> commentService,
        Lazy<GroupMembershipService> groupMembershipService,
        IHttpContextAccessor httpContextAccessor,
        NicknameCacheService nicknameCacheService,
        IEventPublisher eventPublisher)

    {
        private readonly IUserRepository _repo = repo;
        private readonly AppDbContext _db = db;
        private readonly Lazy<PostService> _postService = postService;
        private readonly Lazy<CommentService> _commentService = commentService;
        private readonly Lazy<GroupMembershipService> _groupMembershipService = groupMembershipService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly NicknameCacheService _nicknameCacheService = nicknameCacheService;
        private readonly IEventPublisher _eventPublisher = eventPublisher;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task EnsureUserExistsAsync(long id, string notFoundMessage = "User not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsUserByIdAsync(id))
                throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        public async Task<UserGetResponseDto> GetUserByIdOrThrowAsync(long id, string notFoundMessage = "User not found")
        {
            var user = await GetUserByIdAsync(id);
            if (user == null)
                throw new EntityNotFoundException(notFoundMessage);
            return user;
        }

        private async Task<User> GetUserEntityOrThrowAsync(long id, string notFoundMessage = "User not found")
        {
            var user = await _repo.GetUserByIdAsync(id);
            if (user == null)
                throw new EntityNotFoundException(notFoundMessage);
            return user;
        }

        public async Task<List<UserGetResponseDto>> GetAllUsersAsync()
        {
            var users = await _repo.GetAllUsersAsync();
            return [.. users.Select(MapToDto)];
        }

        public async Task<UserGetResponseDto?> GetUserByIdAsync(long id)
        {
            var user = await _repo.GetUserByIdAsync(id);
            return user == null ? null : MapToDto(user);
        }

        public async Task<UserGetResponseDto?> GetUserByNicknameAsync(string nickname)
        {
            var user = await _repo.GetUserByNicknameAsync(nickname);
            return user == null ? null : MapToDto(user);
        }

        public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(IEnumerable<long> userIds) =>
            _nicknameCacheService.GetNicknamesByUserIdsAsync(userIds);

        private static UserGetResponseDto MapToDto(User user) => new(
            Id: user.Id,
            Email: user.Email,
            Nickname: user.Nickname,
            FriendsListVisibility: user.FriendsListVisibility,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );

        public async Task<User> GetLoggedInUserEntityOrThrowAsync()
        {
            var userId = GetUserIdFromLogin();
            var user = await _repo.GetUserByIdAsync(userId);
            if (user is null)
                throw new UnauthorizedAccessException("Unauthorized access");
            return user;
        }

        public async Task<UserPatchResponseDto> UpdateUserDetailAsync(UserPatchRequestDto request)
        {
            var user = await GetLoggedInUserEntityOrThrowAsync();
            if (request.Id != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            if (!string.Equals(request.Nickname, user.Nickname, StringComparison.Ordinal))
            {
                var isNicknameDuplicate = await _repo.ExistsUserByNicknameAsync(request.Nickname);
                if (isNicknameDuplicate)
                    throw new EntityAlreadyExistsException($"A user with nickname '{request.Nickname}' already exists.");
            }

            var previousNickname = user.Nickname;
            user.UpdateNickname(request.Nickname);
            await _repo.UpdateUserAsync(user);
            await _nicknameCacheService.InvalidateUserNicknameAsync(user.Id);
            if (!string.Equals(previousNickname, user.Nickname, StringComparison.Ordinal))
            {
                await _eventPublisher.PublishAsync(
                    RedisEventChannels.UserNicknameChanged,
                    new UserNicknameChangedEvent(
                        user.Id,
                        user.Nickname,
                        IsDeleted: false,
                        DateTimeOffset.UtcNow));
            }
            return new UserPatchResponseDto(
                Nickname: user.Nickname,
                UpdatedAt: user.UpdatedAt
            );
        }

        public async Task<FriendsListVisibility> GetFriendsListVisibilityAsync(long userId)
        {
            var user = await GetUserEntityOrThrowAsync(userId);
            return user.FriendsListVisibility;
        }

        public async Task<UserPrivacySettingsResponseDto> UpdatePrivacySettingsAsync(UserPrivacySettingsUpdateRequestDto request)
        {
            var user = await GetLoggedInUserEntityOrThrowAsync();
            user.UpdateFriendsListVisibility(request.FriendsListVisibility);
            await _repo.UpdateUserAsync(user);
            return new UserPrivacySettingsResponseDto(
                FriendsListVisibility: user.FriendsListVisibility,
                UpdatedAt: user.UpdatedAt);
        }

        public async Task DeleteUserAsync(long id)
        {
            var userFromLogin = await GetLoggedInUserEntityOrThrowAsync();
            if (id != userFromLogin.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _postService.Value.DetachAuthorFromDeletedUserAsync(id);
                await _commentService.Value.DetachAuthorFromDeletedUserAsync(id);
                await _groupMembershipService.Value.HandleUserDeletionAsync(id);
                await _repo.DeleteUserAsync(userFromLogin);
            });

            await _nicknameCacheService.InvalidateUserNicknameAsync(id);
            await _eventPublisher.PublishAsync(
                RedisEventChannels.UserNicknameChanged,
                new UserNicknameChangedEvent(
                    id,
                    Nickname: null,
                    IsDeleted: true,
                    DateTimeOffset.UtcNow));
        }
    }
}
