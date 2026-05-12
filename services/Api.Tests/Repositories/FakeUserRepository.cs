using Api.Domain.Users.Domain;
using Api.Domain.Users.Repository;

namespace Api.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private long _nextId = 1;
    private readonly Dictionary<long, User> _users = new();

    public Task CreateUserAsync(User user)
    {
        var id = _nextId++;
        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);
        _users[id] = user;
        return Task.CompletedTask;
    }

    public Task<List<User>?> GetAllUsersAsync() => Task.FromResult<List<User>?>(_users.Values.ToList());

    public Task<User?> GetUserByIdAsync(long id)
        => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

    public Task<User?> GetUserByEmailAsync(string email)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));

    public Task<bool> ExistsUserByEmailAsync(string email)
        => Task.FromResult(_users.Values.Any(u => u.Email == email));

    public Task<User?> GetUserByNicknameAsync(string nickname)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Nickname == nickname));

    public Task<bool> ExistsUserByNicknameAsync(string nickname)
        => Task.FromResult(_users.Values.Any(u => u.Nickname == nickname));

    public Task UpdateUserAsync(User user)
    {
        if (user.Id != 0)
        {
            _users[user.Id] = user;
        }
        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(User user)
    {
        if (user.Id != 0)
            _users.Remove(user.Id);
        return Task.CompletedTask;
    }
}