using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class NicknameCacheServiceUnitTests
{
    [Fact]
    public async Task GetNicknamesByUserIdsAsync_LoadsMissingNicknamesFromRepoAndCachesThem()
    {
        // Arrange
        var userRepository = new FakeUserRepository();
        var cache = new FakeDistributedCache();
        var service = DomainServiceTestFactory.CreateNicknameCacheService(userRepository, cache);
        var first = new User("first@example.com", "hash", "first");
        var second = new User("second@example.com", "hash", "second");
        await userRepository.CreateUserAsync(first);
        await userRepository.CreateUserAsync(second);

        // Act
        var nicknames = await service.GetNicknamesByUserIdsAsync([first.Id, second.Id]);
        var cachedNicknames = await service.GetNicknamesByUserIdsAsync([first.Id, second.Id]);

        // Assert
        Assert.Equal("first", nicknames[first.Id]);
        Assert.Equal("second", nicknames[second.Id]);
        Assert.Equal(nicknames, cachedNicknames);
    }
}
