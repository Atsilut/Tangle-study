using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task CreateGroup_Returns201_AndOwnerIsCreator()
    {
        // Arrange
        const string testMethodName = "GroupCreate";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);

        // Act
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        // Assert
        Assert.Equal(1, group.MemberCount);

        var membersRes = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");
        await IntegrationAssertions.AssertStatusAsync(membersRes, HttpStatusCode.OK);
        var members = await membersRes.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.NotNull(members);
        var single = Assert.Single(members);
        Assert.Equal(GroupRole.Owner, single.Role);
        Assert.Equal(owner.Id, single.UserId);
    }
}
