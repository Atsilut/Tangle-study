using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupInvitationControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private const string Password = "testtest123!";

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, long index = 1)
    {
        var email = $"{testMethodName}{index}@test.com";
        var nickname = $"{testMethodName}User{index}";
        var req = new UserCreateRequestDto { Email = email, Password = Password, Nickname = nickname };
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    private async Task LoginAs(UserGetResponseDto user)
    {
        var req = new LoginRequestDto { Email = user.Email, Password = Password };
        var login = await Client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private async Task<GroupResponseDto> CreateGroupAs(UserGetResponseDto user)
    {
        await LoginAs(user);
        var res = await Client.PostAsJsonAsync("/api/groups", new GroupCreateRequestDto
        {
            Name = $"G_{Guid.NewGuid():N}".Substring(0, 20),
            Description = "test",
            Visibility = GroupVisibility.Private,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupResponseDto>())!;
    }

    // --- INVITE FLOW ---

    [Fact]
    public async Task InviteAcceptFlow_AddsInviteeAsMember()
    {
        var owner = await CreateUserForTest("Invite", 1);
        var invitee = await CreateUserForTest("Invite", 2);
        var group = await CreateGroupAs(owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await LoginAs(invitee);
        var accept = await Client.PostAsync($"/api/invitations/{invitation!.Id}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, m => m.UserId == invitee.Id && m.Role == GroupRole.Member);
    }

    [Fact]
    public async Task InviteAfterApply_Returns200_AndAddsMember()
    {
        var owner = await CreateUserForTest("InviteRecip", 1);
        var applicant = await CreateUserForTest("InviteRecip", 2);
        var group = await CreateGroupAs(owner);

        await LoginAs(applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        await LoginAs(owner);
        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = applicant.Id });

        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == applicant.Id && m.Role == GroupRole.Member);

        await LoginAs(applicant);
        var mine = await Client.GetAsync("/api/invitations/me");
        Assert.Equal(HttpStatusCode.NoContent, mine.StatusCode);
    }

    [Fact]
    public async Task Invite_Returns401_WhenCallerNotAdmin()
    {
        var owner = await CreateUserForTest("InviteAuth", 1);
        var invitee = await CreateUserForTest("InviteAuth", 2);
        var stranger = await CreateUserForTest("InviteAuth", 3);
        var group = await CreateGroupAs(owner);

        await LoginAs(stranger);
        var res = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetMyInvitations_ListsPending()
    {
        var owner = await CreateUserForTest("InviteMe", 1);
        var invitee = await CreateUserForTest("InviteMe", 2);
        var group = await CreateGroupAs(owner);
        await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        await LoginAs(invitee);
        var res = await Client.GetAsync("/api/invitations/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>();
        var only = Assert.Single(list!);
        Assert.Equal(group.Id, only.GroupId);
    }

    [Fact]
    public async Task Cancel_DeletesInvitation()
    {
        var owner = await CreateUserForTest("InviteCancel", 1);
        var invitee = await CreateUserForTest("InviteCancel", 2);
        var group = await CreateGroupAs(owner);
        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        var del = await Client.DeleteAsync($"/api/invitations/{invitation!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        await LoginAs(invitee);
        var mine = await Client.GetAsync("/api/invitations/me");
        Assert.Equal(HttpStatusCode.NoContent, mine.StatusCode);
    }

    [Fact]
    public async Task IgnoreThenAccept_AddsMember()
    {
        var owner = await CreateUserForTest("InviteIgnore", 1);
        var invitee = await CreateUserForTest("InviteIgnore", 2);
        var group = await CreateGroupAs(owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await LoginAs(invitee);
        var ignore = await Client.PostAsync($"/api/invitations/{invitation!.Id}/ignore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);

        var accept = await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        await LoginAs(owner);
        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == invitee.Id && m.Role == GroupRole.Member);
    }

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutReactivate_WhenResendingAfterInviteeIgnored()
    {
        var owner = await CreateUserForTest("InviteResend", 1);
        var invitee = await CreateUserForTest("InviteResend", 2);
        var group = await CreateGroupAs(owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await LoginAs(invitee);
        await Client.PostAsync($"/api/invitations/{invitation!.Id}/ignore", content: null);

        await LoginAs(owner);
        var resend = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        var dto = await resend.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();
        Assert.True(dto!.IsPending);
        Assert.Equal(invitation.Id, dto.Id);
    }

    [Fact]
    public async Task GetIgnoredIncoming_Returns204_WhenEmpty()
    {
        var invitee = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredIncoming_Returns204_WhenEmpty));
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, invitee);

        var res = await Client.GetAsync("/api/invitations/ignored");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetIgnoredIncoming_ListsIgnored_ForInvitee()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredIncoming_ListsIgnored_ForInvitee), 1);
        var invitee = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredIncoming_ListsIgnored_ForInvitee), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, invitee);
        await Client.PostAsync($"/api/invitations/{invitation!.Id}/ignore", content: null);

        var res = await Client.GetAsync("/api/invitations/ignored");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>();
        var only = Assert.Single(list!);
        Assert.Equal(invitation.Id, only.Id);
        Assert.False(only.IsPending);
        Assert.True(only.IsIncoming);
    }

    [Fact]
    public async Task GetMyInvitations_ShowsMaskedPending_ForInviterAfterInviteeIgnored()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetMyInvitations_ShowsMaskedPending_ForInviterAfterInviteeIgnored), 1);
        var invitee = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetMyInvitations_ShowsMaskedPending_ForInviterAfterInviteeIgnored), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, invitee);
        await Client.PostAsync($"/api/invitations/{invitation!.Id}/ignore", content: null);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var res = await Client.GetAsync("/api/invitations/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>();
        var only = Assert.Single(list!);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task Invite_Returns400_WhenInviterBlockedInvitee()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Invite_Returns400_WhenInviterBlockedInvitee), 1);
        var invitee = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Invite_Returns400_WhenInviterBlockedInvitee), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.BlockUserAsync(Client, invitee.Id);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(HttpStatusCode.BadRequest, invite.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns400_WhenInviteeBlockedInviter()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Accept_Returns400_WhenInviteeBlockedInviter), 1);
        var invitee = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Accept_Returns400_WhenInviteeBlockedInviter), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, invitee);
        await GroupIntegrationTestHelpers.BlockUserAsync(Client, owner.Id);
        await Client.PostAsync($"/api/invitations/{invitation!.Id}/ignore", content: null);

        var accept = await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, accept.StatusCode);
    }
}
