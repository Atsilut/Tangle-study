using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupJoinResolutionServiceUnitTests
{
    [Fact]
    public async Task CreateMembershipFromJoinRequests_AddsMemberRole()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.InMemoryUser.SeedUser("owner");
        var strangerId = graph.InMemoryUser.SeedUser("stranger");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = GroupJoinPolicy.Open,
        });

        await graph.GroupJoinResolutionService.CreateMembershipFromJoinRequestsAsync(group.Id, strangerId);

        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, strangerId);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
    }
}
