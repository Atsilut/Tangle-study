using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupInvitationServiceUnitTests
{
    [Fact]
    public async Task Invite_CreatesPendingInvitation()
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
            JoinPolicy = GroupJoinPolicy.InvitationOnly,
        });

        var result = await graph.GroupInvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = strangerId });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(await graph.GroupInvitationRepository.GetPendingForUserAsync(group.Id, strangerId));
    }
}
