using Api.Domain.UserBlocks.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class UserBlockServiceUnitTests
{
    [Fact]
    public async Task BlockUser_CreatesBlock()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var blocker = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "blocker");
        var blocked = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "blocked");
        http.HttpContext = ServiceTestHelpers.ContextFor(blocker.Id);

        // Act
        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Assert
        Assert.True(await graph.UserBlockRepository.ExistsUserBlockAsync(blocker.Id, blocked.Id));
    }
}
