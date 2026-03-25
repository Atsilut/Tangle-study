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

    public Task<List<User>?> GetAllAsync() => Task.FromResult<List<User>?>(_users.Values.ToList());

    public Task<User?> GetByIdAsync(long id)
        => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

    public Task<User?> GetByEmailAsync(string email)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));

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