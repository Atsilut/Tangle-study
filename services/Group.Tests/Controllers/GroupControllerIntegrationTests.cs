using System.Net;
using System.Net.Http.Json;
using Group.Entities;
using Group.Dto;
using Group.Tests.Infrastructure;

namespace Group.Tests.Controllers;

[Collection(GroupIntegrationTestCollection.Name)]
public sealed class GroupControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task CreateGroup_Returns201_AndOwnerIsCreator()
    {
        // Arrange
        const string testMethodName = "GroupCreate";
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 1);

        // Act
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        // Assert
        Assert.Equal(1, group.MemberCount);

        var membersRes = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(membersRes, HttpStatusCode.OK);
        var members = await membersRes.Content.ReadFromJsonAsync<List<GroupMemberGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(members);
        var single = Assert.Single(members);
        Assert.Equal(GroupRole.Owner, single.Role);
        Assert.Equal(owner.Id, single.UserId);
    }

    [Fact]
    public async Task ListDiscoverable_ReturnsOnlyPublicGroups()
    {
        const string testMethodName = nameof(ListDiscoverable_ReturnsOnlyPublicGroups);

        // Arrange
        var user = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 1);
        var publicGroup = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            user,
            GroupVisibility.Public,
            GroupJoinPolicy.Open);
        await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            user,
            GroupVisibility.Private,
            GroupJoinPolicy.Requestable);
        GroupIntegrationTestHelpers.LoginAs(Client, user);

        // Act
        var res = await Client.GetAsync(GroupIntegrationTestHelpers.GroupsBase, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var groups = await res.Content.ReadFromJsonAsync<List<GroupGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(groups);
        var listed = Assert.Single(groups);
        Assert.Equal(publicGroup.Id, listed.Id);
        Assert.Equal(GroupVisibility.Public, listed.Visibility);
    }

    [Fact]
    public async Task ListDiscoverable_Returns204_WhenNoPublicGroups()
    {
        const string testMethodName = nameof(ListDiscoverable_Returns204_WhenNoPublicGroups);

        // Arrange
        var user = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 1);
        await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, user, GroupVisibility.Private);
        GroupIntegrationTestHelpers.LoginAs(Client, user);

        // Act
        var res = await Client.GetAsync(GroupIntegrationTestHelpers.GroupsBase, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMyGroups_ReturnsMemberships_IncludingPrivate()
    {
        const string testMethodName = nameof(ListMyGroups_ReturnsMemberships_IncludingPrivate);

        // Arrange
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 1);
        var member = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 2);
        var publicGroup = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            owner,
            GroupVisibility.Public);
        var privateGroup = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            owner,
            GroupVisibility.Private);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, publicGroup.Id, member.Id, GroupRole.Member);
        GroupIntegrationTestHelpers.LoginAs(Client, member);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/me", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var groups = await res.Content.ReadFromJsonAsync<List<GroupGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(groups);
        Assert.Single(groups);
        Assert.Equal(publicGroup.Id, groups[0].Id);
        Assert.Equal(2, groups[0].MemberCount);
    }

    [Fact]
    public async Task ListMyGroups_Returns204_WhenNotMemberOfAnyGroup()
    {
        const string testMethodName = nameof(ListMyGroups_Returns204_WhenNotMemberOfAnyGroup);

        // Arrange
        var user = GroupIntegrationTestHelpers.CreateUser(Factory, testMethodName, 1);
        GroupIntegrationTestHelpers.LoginAs(Client, user);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/me", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }
}
