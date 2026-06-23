using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class GroupJoinResolutionServiceUnitTests
{
    [Fact]
    public async Task CreateMembershipFromJoinRequests_AddsMemberRole()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var stranger = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "stranger");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = GroupJoinPolicy.Open,
        });

        // Act
        await graph.GroupJoinResolutionService.CreateMembershipFromJoinRequestsAsync(group.Id, stranger.Id);

        // Assert
        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, stranger.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
    }
}
