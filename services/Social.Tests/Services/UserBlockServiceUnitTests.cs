using Social.Tests.Infrastructure;
using Social.Dto;

namespace Social.Tests.Services;

public sealed class UserBlockServiceUnitTests
{
    [Fact]
    public async Task BlockUser_CreatesBlock()
    {
        using var graph = SocialServiceTestFactory.Create();
        var blocker = graph.SeedUser("blocker");
        var blocked = graph.SeedUser("blocked");
        graph.SetCaller(blocker);

        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked });

        Assert.True(await graph.UserBlockRepository.ExistsUserBlockAsync(blocker, blocked));
    }
}
