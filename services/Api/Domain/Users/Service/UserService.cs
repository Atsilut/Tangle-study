using Api.Domain.Comments.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Api.Domain.Users.Service
{
    [Service]
    public class UserService
    {
        private readonly IUserRepository _repo;
        private readonly Lazy<PostService> _postService;
        private readonly Lazy<CommentService> _commentService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(
            IUserRepository repo,
            Lazy<PostService> postService,
            Lazy<CommentService> commentService,
            IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _postService = postService;
            _commentService = commentService;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized access"));

        public async Task EnsureUserExistsAsync(long id, string notFoundMessage = "User not found")
        {
            if (!await _repo.ExistsUserByIdAsync(id))
                throw new EntityNotFoundException(notFoundMessage);
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
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );

        public async Task<UserPatchResponseDto?> UpdateUserDetailAsync(UserPatchRequestDto request)
        {
            var user = await GetUserEntityOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            if (request.Id != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            var isNicknameDuplicate = await _repo.ExistsUserByNicknameAsync(request.Nickname);
            if (isNicknameDuplicate) throw new EntityAlreadyExistsException("User already exists with nickname : ", request.Nickname);
            user.UpdateNickname(request.Nickname);
            await _repo.UpdateUserAsync(user);
            return new UserPatchResponseDto(
                Nickname: user.Nickname,
                UpdatedAt: user.UpdatedAt
            );
        }

        public async Task DeleteUserAsync(long id)
        {
            var userFromLogin = await GetUserEntityOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            if (id != userFromLogin.Id) throw new UnauthorizedAccessException("Unauthorized access");

            var user = await GetUserEntityOrThrowAsync(id);
            await _postService.Value.DetachAuthorFromDeletedUserAsync(id);
            await _commentService.Value.DetachAuthorFromDeletedUserAsync(id);
            await _repo.DeleteUserAsync(user);
        }
    }
}
