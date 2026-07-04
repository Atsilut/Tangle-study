using Api.Client;
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
        ICommunityClient communityClient,
        IMediaClient mediaClient,
        IChatClient chatClient,
        ILocationClient locationClient,
        IGroupClient groupClient,
        ISocialClient socialClient,
        IHttpContextAccessor httpContextAccessor,
        NicknameCacheService nicknameCacheService,
        IEventPublisher eventPublisher)

    {
        private readonly IUserRepository _repo = repo;
        private readonly AppDbContext _db = db;
        private readonly ICommunityClient _communityClient = communityClient;
        private readonly IMediaClient _mediaClient = mediaClient;
        private readonly IChatClient _chatClient = chatClient;
        private readonly ILocationClient _locationClient = locationClient;
        private readonly IGroupClient _groupClient = groupClient;
        private readonly ISocialClient _socialClient = socialClient;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly NicknameCacheService _nicknameCacheService = nicknameCacheService;
        private readonly IEventPublisher _eventPublisher = eventPublisher;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task EnsureUserExistsAsync(long id, string notFoundMessage = "User not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsUserByIdAsync(id)) throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        public async Task EnsureUsersExistAsync(
            IReadOnlyCollection<long> ids,
            string notFoundMessage = "User not found",
            int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.AllUsersExistByIdsAsync(ids)) throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        public async Task<UserGetResponseDto> GetUserByIdOrThrowAsync(long id, string notFoundMessage = "User not found") =>
            await GetUserByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage);

        private async Task<User> GetUserEntityOrThrowAsync(long id, string notFoundMessage = "User not found") =>
            await _repo.GetUserByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage);

        public async Task<List<UserGetResponseDto>> GetAllUsersAsync()
        {
            var users = await _repo.GetAllUsersAsync();
            return [.. users.Select(u => MapToDto(u, includeEmail: false))];
        }

        public async Task<UserGetResponseDto?> GetUserByIdAsync(long id)
        {
            var user = await _repo.GetUserByIdAsync(id);
            if (user == null) return null;

            var viewerId = TryGetUserIdFromLogin();
            return MapToDto(user, includeEmail: viewerId == id);
        }

        public async Task<UserGetResponseDto?> GetUserByNicknameAsync(string nickname)
        {
            var user = await _repo.GetUserByNicknameAsync(nickname);
            if (user == null) return null;

            var viewerId = TryGetUserIdFromLogin();
            return MapToDto(user, includeEmail: viewerId == user.Id);
        }

        public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(IEnumerable<long> userIds) =>
            _nicknameCacheService.GetNicknamesByUserIdsAsync(userIds);

        private long? TryGetUserIdFromLogin()
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) ? id : null;
        }

        private static UserGetResponseDto MapToDto(User user, bool includeEmail) => new(
            Id: user.Id,
            Email: includeEmail ? user.Email : null,
            Nickname: user.Nickname,
            FriendsListVisibility: user.FriendsListVisibility,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );

        public async Task<User> GetLoggedInUserEntityOrThrowAsync()
        {
            var userId = GetUserIdFromLogin();
            return await _repo.GetUserByIdAsync(userId) ?? throw new UnauthorizedAccessException("Unauthorized access");
        }

        public async Task<UserPatchResponseDto> UpdateUserDetailAsync(UserPatchRequestDto request)
        {
            var user = await GetLoggedInUserEntityOrThrowAsync();
            if (request.Id != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            if (!string.Equals(request.Nickname, user.Nickname, StringComparison.Ordinal))
            {
                var isNicknameDuplicate = await _repo.ExistsUserByNicknameAsync(request.Nickname);
                if (isNicknameDuplicate) throw new EntityAlreadyExistsException($"A user with nickname '{request.Nickname}' already exists.");
            }

            var previousNickname = user.Nickname;
            user.UpdateNickname(request.Nickname);
            await _repo.UpdateUserAsync(user);
            await _nicknameCacheService.InvalidateUserNicknameAsync(user.Id);
            if (!string.Equals(previousNickname, user.Nickname, StringComparison.Ordinal))
                await _eventPublisher.PublishAsync(
                    RedisEventChannels.UserNicknameChanged,
                    new UserNicknameChangedEvent(
                        user.Id,
                        user.Nickname,
                        IsDeleted: false,
                        DateTimeOffset.UtcNow));
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
                await _communityClient.DetachUserOnDeletionAsync(id);
                await _mediaClient.DetachUploaderFromDeletedUserAsync(id);
                await _chatClient.DetachUserOnDeletionAsync(id);
                await _groupClient.DetachUserOnDeletionAsync(id);
                await _socialClient.DetachUserOnDeletionAsync(id);
                await _locationClient.DetachUserOnDeletionAsync(id);
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
