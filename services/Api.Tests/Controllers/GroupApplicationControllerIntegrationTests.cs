using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupApplicationControllerIntegrationTests(PostgresTestcontainerFixture postgres)
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

    private async Task<GroupResponseDto> CreateGroupAs(UserGetResponseDto user, GroupVisibility visibility = GroupVisibility.Public)
    {
        await LoginAs(user);
        var res = await Client.PostAsJsonAsync("/api/groups", new GroupCreateRequestDto
        {
            Name = $"G_{Guid.NewGuid():N}".Substring(0, 20),
            Description = "test",
            Visibility = visibility,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupResponseDto>())!;
    }

    [Fact]
    public async Task ApplyApproveFlow_AddsApplicantAsMember()
    {
        var owner = await CreateUserForTest("Apply", 1);
        var applicant = await CreateUserForTest("Apply", 2);
        var group = await CreateGroupAs(owner);

        await LoginAs(applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        await LoginAs(owner);
        var approve = await Client.PostAsync($"/api/applications/{application!.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == applicant.Id && m.Role == GroupRole.Member);
    }

    [Fact]
    public async Task RejectApplication_DoesNotAddMember()
    {
        var owner = await CreateUserForTest("ApplyReject", 1);
        var applicant = await CreateUserForTest("ApplyReject", 2);
        var group = await CreateGroupAs(owner);

        await LoginAs(applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        await LoginAs(owner);
        var reject = await Client.PostAsync($"/api/applications/{application!.Id}/reject", content: null);
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.DoesNotContain(list!, m => m.UserId == applicant.Id);
    }

    [Fact]
    public async Task ApplyAfterInvite_Returns200_AndAddsMember()
    {
        var owner = await CreateUserForTest("ApplyRecip", 1);
        var applicant = await CreateUserForTest("ApplyRecip", 2);
        var group = await CreateGroupAs(owner);

        var invite = await Client.PostAsJsonAsync($"/api/groups/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = applicant.Id });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        await LoginAs(applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);

        await LoginAs(owner);
        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == applicant.Id && m.Role == GroupRole.Member);

        var pending = await Client.GetAsync($"/api/groups/{group.Id}/applications");
        var applications = await pending.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>();
        Assert.Empty(applications!);
    }

    [Fact]
    public async Task Cancel_DeletesApplication()
    {
        var owner = await CreateUserForTest("ApplyCancel", 1);
        var applicant = await CreateUserForTest("ApplyCancel", 2);
        var group = await CreateGroupAs(owner);

        await LoginAs(applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        var del = await Client.DeleteAsync($"/api/applications/{application!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        await LoginAs(owner);
        var pending = await Client.GetAsync($"/api/groups/{group.Id}/applications");
        var list = await pending.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>();
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Ignore_Returns204_AndApproveStillWorks()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Ignore_Returns204_AndApproveStillWorks), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Ignore_Returns204_AndApproveStillWorks), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var ignore = await Client.PostAsync($"/api/applications/{application!.Id}/ignore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);

        var approve = await Client.PostAsync($"/api/applications/{application.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var members = await Client.GetAsync($"/api/groups/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == applicant.Id && m.Role == GroupRole.Member);
    }

    [Fact]
    public async Task GetMyApplications_ShowsMaskedPending_AfterAdminIgnored()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetMyApplications_ShowsMaskedPending_AfterAdminIgnored), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetMyApplications_ShowsMaskedPending_AfterAdminIgnored), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        await Client.PostAsync($"/api/applications/{application!.Id}/ignore", content: null);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var res = await Client.GetAsync("/api/applications/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>();
        var only = Assert.Single(list!);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task GetIgnoredApplications_Returns204_WhenEmpty()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredApplications_Returns204_WhenEmpty));
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var res = await Client.GetAsync($"/api/groups/{group.Id}/applications/ignored");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetIgnoredApplications_ListsIgnored_ForAdmin()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredApplications_ListsIgnored_ForAdmin), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(GetIgnoredApplications_ListsIgnored_ForAdmin), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"/api/groups/{group.Id}/applications", content: null);
        var application = await apply.Content.ReadFromJsonAsync<GroupApplicationResponseDto>();

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        await Client.PostAsync($"/api/applications/{application!.Id}/ignore", content: null);

        var res = await Client.GetAsync($"/api/groups/{group.Id}/applications/ignored");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>();
        var only = Assert.Single(list!);
        Assert.Equal(application.Id, only.Id);
        Assert.False(only.IsPending);
        Assert.True(only.IsIncoming);
    }
}
