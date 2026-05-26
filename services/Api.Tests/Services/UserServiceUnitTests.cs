using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Service;

public sealed class UserServiceUnitTests
{
    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenMissing()
    {
        var graph = DomainServiceTestFactory.Create();
        var dto = await graph.UserService.GetUserByIdAsync(12345);
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        var graph = DomainServiceTestFactory.Create();
        var user = new User("a@a.com", "password", "old");
        await graph.UserRepository.CreateUserAsync(user);

        var res = await graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, "new"));

        Assert.NotNull(res);
        Assert.Equal("new", res!.Nickname);
    }
}
