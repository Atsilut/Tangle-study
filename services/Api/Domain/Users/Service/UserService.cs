using Api.Domain.Comments.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Api.Domain.Users.Service
{
    [Service]
    public class UserService
    {
        private readonly IUserRepository _repo;
        private readonly AppDbContext _db;
        private readonly Lazy<PostService> _postService;
        private readonly Lazy<CommentService> _commentService;
        private readonly Lazy<GroupMembershipService> _groupMembershipService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(
            IUserRepository repo,
            AppDbContext db,
            Lazy<PostService> postService,
            Lazy<CommentService> commentService,
            Lazy<GroupMembershipService> groupMembershipService,
            IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _db = db;
            _postService = postService;
            _commentService = commentService;
            _groupMembershipService = groupMembershipService;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized access"));

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
            return users.Select(MapToDto).ToList();
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

        public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(IEnumerable<long> userIds) =>
            await _repo.GetNicknamesByIdsAsync(userIds);

        private static UserGetResponseDto MapToDto(User user) => new(
            Id: user.Id,
            Email: user.Email,
            Nickname: user.Nickname,
            FriendsListVisibility: user.FriendsListVisibility,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );

        public async Task<UserPatchResponseDto> UpdateUserDetailAsync(UserPatchRequestDto request)
        {
            var user = await GetUserEntityOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            if (request.Id != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            if (!string.Equals(request.Nickname, user.Nickname, StringComparison.Ordinal))
            {
                var isNicknameDuplicate = await _repo.ExistsUserByNicknameAsync(request.Nickname);
                if (isNicknameDuplicate)
                    throw new EntityAlreadyExistsException($"A user with nickname '{request.Nickname}' already exists.");
            }
            user.UpdateNickname(request.Nickname);
            await _repo.UpdateUserAsync(user);
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
            var user = await GetUserEntityOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            user.UpdateFriendsListVisibility(request.FriendsListVisibility);
            await _repo.UpdateUserAsync(user);
            return new UserPrivacySettingsResponseDto(
                FriendsListVisibility: user.FriendsListVisibility,
                UpdatedAt: user.UpdatedAt);
        }

        public async Task DeleteUserAsync(long id)
        {
            var userFromLogin = await GetUserEntityOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            if (id != userFromLogin.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _postService.Value.DetachAuthorFromDeletedUserAsync(id);
                await _commentService.Value.DetachAuthorFromDeletedUserAsync(id);
                await _groupMembershipService.Value.HandleUserDeletionAsync(id);
                await _repo.DeleteUserAsync(userFromLogin);
            });
        }
    }
}
