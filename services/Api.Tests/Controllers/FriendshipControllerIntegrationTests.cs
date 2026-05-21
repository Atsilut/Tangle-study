using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FriendshipControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private const string Password = "testtest123!";

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, long index = 1)
    {
        var email = $"{testMethodName}{index}@test.com";
        var nickname = $"{testMethodName}User{index}";
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = Password,
            Nickname = nickname,
        };
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

    // --- SEND ---

    [Fact]
    public async Task SendRequest_Returns201_AndPendingFriendship()
    {
        const string testMethodName = "FriendSend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(FriendshipStatus.Pending, body.Status);
        Assert.Equal(addressee.Id, body.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns401_WhenNotAuthenticated()
    {
        const string testMethodName = "FriendSendUnauth";
        var addressee = await CreateUserForTest(testMethodName, 1);
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeMissing()
    {
        const string testMethodName = "FriendSendMissing";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = 999999 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeIsSelf()
    {
        const string testMethodName = "FriendSendSelf";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = requester.Id });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns409_WhenAlreadyExists()
    {
        const string testMethodName = "FriendSendDup";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await LoginAs(a);
        var first = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = b.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var dup = await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = b.Id });

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // --- ACCEPT / REJECT ---

    [Fact]
    public async Task Accept_Returns200_AndTransitionsToAccepted()
    {
        const string testMethodName = "FriendAccept";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        var created = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = addressee.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();

        await LoginAs(addressee);
        var res = await Client.PostAsync($"/api/friendships/{created!.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();
        Assert.Equal(FriendshipStatus.Accepted, body!.Status);
    }

    [Fact]
    public async Task Accept_Returns401_WhenCalledByRequester()
    {
        const string testMethodName = "FriendAcceptUnauth";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        var created = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = addressee.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();

        var res = await Client.PostAsync($"/api/friendships/{created!.Id}/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Reject_Returns200()
    {
        const string testMethodName = "FriendReject";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        var created = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = addressee.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();

        await LoginAs(addressee);
        var res = await Client.PostAsync($"/api/friendships/{created!.Id}/reject", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();
        Assert.Equal(FriendshipStatus.Rejected, body!.Status);
    }

    // --- REMOVE ---

    [Fact]
    public async Task Remove_Returns204_AndDeletesFriendship()
    {
        const string testMethodName = "FriendRemove";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await LoginAs(a);
        var created = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = b.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();

        var res = await Client.DeleteAsync($"/api/friendships/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var pending = await Client.GetAsync("/api/friendships/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task Remove_Returns401_WhenStranger()
    {
        const string testMethodName = "FriendRemoveStranger";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await LoginAs(a);
        var created = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = b.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();

        await LoginAs(stranger);
        var res = await Client.DeleteAsync($"/api/friendships/{created!.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- LIST ---

    [Fact]
    public async Task GetMyFriends_ReturnsAccepted()
    {
        const string testMethodName = "FriendList";
        var me = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var pendingUser = await CreateUserForTest(testMethodName, 3);

        await LoginAs(me);
        var accepted = await (await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = friend.Id }))
            .Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();
        await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = pendingUser.Id });

        await LoginAs(friend);
        await Client.PostAsync($"/api/friendships/{accepted!.Id}/accept", content: null);

        await LoginAs(me);
        var res = await Client.GetAsync("/api/friendships/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        var only = Assert.Single(list!);
        Assert.Equal(friend.Id, only.OtherUserId);
    }

    [Fact]
    public async Task GetPending_ListsIncomingAndOutgoing()
    {
        const string testMethodName = "FriendPending";
        var me = await CreateUserForTest(testMethodName, 1);
        var outgoing = await CreateUserForTest(testMethodName, 2);
        var incoming = await CreateUserForTest(testMethodName, 3);

        await LoginAs(me);
        await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = outgoing.Id });
        await LoginAs(incoming);
        await Client.PostAsJsonAsync("/api/friendships", new FriendshipRequestCreateRequestDto { AddresseeId = me.Id });

        await LoginAs(me);
        var res = await Client.GetAsync("/api/friendships/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(list, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }
}
