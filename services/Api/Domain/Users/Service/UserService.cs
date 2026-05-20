using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Users.Service
{
    [Service]
    public class UserService
    {
        private readonly IUserRepository _repo;

        public UserService(IUserRepository repo)
        {
            _repo = repo;
        }

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

            var list = new List<UserGetResponseDto>();
            foreach (var user in users)
            {
                list.Add(new UserGetResponseDto(
                    Id: user.Id,
                    Email: user.Email,
                    Nickname: user.Nickname,
                    CreatedAt: user.CreatedAt,
                    UpdatedAt: user.UpdatedAt
                ));
            }

            return list;
        }

        public async Task<UserGetResponseDto?> GetUserByIdAsync(long id)
        {
            var user = await _repo.GetUserByIdAsync(id);
            if (user == null) return null;

            var userResponse = new UserGetResponseDto(
                Id: user.Id,
                Email: user.Email,
                Nickname: user.Nickname,
                CreatedAt: user.CreatedAt,
                UpdatedAt: user.UpdatedAt
            );

            return userResponse;
        }

        public async Task<UserGetResponseDto?> GetUserByNicknameAsync(string nickname)
        {
            var user = await _repo.GetUserByNicknameAsync(nickname);
            if (user == null) return null;
            var userResponse = new UserGetResponseDto(
                Id: user.Id,
                Email: user.Email,
                Nickname: user.Nickname,
                CreatedAt: user.CreatedAt,
                UpdatedAt: user.UpdatedAt
            );
            return userResponse;
        }

        public async Task<UserPatchResponseDto?> UpdateUserDetailAsync(UserPatchRequestDto request)
        {
            var user = await GetUserEntityOrThrowAsync(request.Id);
            var isNicknameDuplicate = await _repo.ExistsUserByNicknameAsync(request.Nickname);
            if (isNicknameDuplicate) throw new EntityAlreadyExistsException("User already exists with nickname : ", request.Nickname);
            user.UpdateNickname(request.Nickname);
            await _repo.UpdateUserAsync(user);
            var response = new UserPatchResponseDto(
                Nickname: user.Nickname,
                UpdatedAt: user.UpdatedAt
            );
            return response;
        }

        public async Task DeleteUserAsync(long id)
        {
            var user = await GetUserEntityOrThrowAsync(id);
            await _repo.DeleteUserAsync(user);
        }
    }
}