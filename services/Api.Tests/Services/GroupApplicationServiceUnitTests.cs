using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class GroupApplicationServiceUnitTests
{
    [Fact]
    public async Task Apply_CreatesPendingApplication()
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
            JoinPolicy = GroupJoinPolicy.Requestable,
        });
        http.HttpContext = ServiceTestHelpers.ContextFor(stranger.Id);

        // Act
        var result = await graph.GroupApplicationService.ApplyAsync(group.Id);

        // Assert
        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(await graph.GroupApplicationRepository.GetPendingForUserAsync(group.Id, stranger.Id));
    }
}
