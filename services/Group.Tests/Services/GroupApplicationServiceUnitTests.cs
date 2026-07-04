using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupApplicationServiceUnitTests
{
    [Fact]
    public async Task Apply_CreatesPendingApplication()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.MonolithAccess.SeedUser("owner");
        var strangerId = graph.MonolithAccess.SeedUser("stranger");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = GroupJoinPolicy.Requestable,
        });
        http.HttpContext = FakeHttpContextAccessor.ContextFor(strangerId);

        var result = await graph.GroupApplicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(await graph.GroupApplicationRepository.GetPendingForUserAsync(group.Id, strangerId));
    }
}
