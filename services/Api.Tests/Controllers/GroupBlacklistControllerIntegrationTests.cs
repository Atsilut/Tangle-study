using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupBlacklistControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Add_Returns201_AndKicksMember()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns201_AndKicksMember), 1);
        var target = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns201_AndKicksMember), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, target.Id, GroupRole.Member);

        var add = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = target.Id });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var memberList = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.DoesNotContain(memberList!, m => m.UserId == target.Id);
    }

    [Fact]
    public async Task Add_ClearsPendingInviteAndApplication()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_ClearsPendingInviteAndApplication), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_ClearsPendingInviteAndApplication), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var add = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = applicant.Id });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var pendingApps = await Client.GetAsync($"/api/groups/{group.Id}/applications");
        Assert.Empty(await pendingApps.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>()!);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var mine = await Client.GetAsync("/api/invitations/me");
        Assert.Equal(HttpStatusCode.NoContent, mine.StatusCode);
    }

    [Fact]
    public async Task Add_Returns401_WhenCallerIsMember()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsMember), 1);
        var member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsMember), 2);
        var target = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsMember), 3);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);
        var res = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = target.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Add_Returns401_WhenCallerIsAdmin()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsAdmin), 1);
        var admin = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsAdmin), 2);
        var target = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Add_Returns401_WhenCallerIsAdmin), 3);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, admin.Id, GroupRole.Admin);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, admin);
        var res = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = target.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Invite_Returns400_WhenInviteeBlacklisted()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Invite_Returns400_WhenInviteeBlacklisted), 1);
        var target = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Invite_Returns400_WhenInviteeBlacklisted), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = target.Id });

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = target.Id });

        Assert.Equal(HttpStatusCode.BadRequest, invite.StatusCode);
    }

    [Fact]
    public async Task Apply_Returns400_WhenApplicantBlacklisted()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_Returns400_WhenApplicantBlacklisted), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_Returns400_WhenApplicantBlacklisted), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = applicant.Id });

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
    }

    [Fact]
    public async Task Remove_Returns204()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Remove_Returns204), 1);
        var target = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Remove_Returns204), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await Client.PostAsJsonAsync($"/api/groups/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = target.Id });

        var remove = await Client.DeleteAsync($"/api/groups/{group.Id}/blacklist/{target.Id}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var list = await Client.GetAsync($"/api/groups/{group.Id}/blacklist");
        Assert.Empty(await list.Content.ReadFromJsonAsync<List<GroupBlacklistResponseDto>>()!);
    }
}
