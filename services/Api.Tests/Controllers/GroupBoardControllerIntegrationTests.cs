using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupBoardControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task ListBoards_ReturnsOnlyVisibleBoards_ForStrangerOnPublicGroup()
    {
        const string testMethodName = "GbListStranger";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Public);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var createOpen = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "Open", Visibility = BoardVisibility.ForAll });
        Assert.Equal(HttpStatusCode.Created, createOpen.StatusCode);

        var createAdmin = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "Staff", Visibility = BoardVisibility.AdminOnly });
        Assert.Equal(HttpStatusCode.Created, createAdmin.StatusCode);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var boards = await res.Content.ReadFromJsonAsync<List<GroupBoardResponseDto>>();
        Assert.NotNull(boards);
        Assert.Single(boards);
        Assert.Equal("Open", boards[0].Name);
        Assert.Equal(BoardVisibility.ForAll, boards[0].Visibility);
    }

    [Fact]
    public async Task CreateBoard_Returns401_WhenMemberNotAdmin()
    {
        const string testMethodName = "GbCreateMember";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Public);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);

        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "Denied" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
