using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Users.Domain;
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

    private async Task SetFriendsListVisibilityAsync(UserGetResponseDto user, FriendsListVisibility visibility)
    {
        await LoginAs(user);
        var res = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = visibility });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private async Task SendFriendRequestAsync(long addresseeId)
    {
        var res = await Client.PostAsJsonAsync("/api/friendships",
            new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private async Task<FriendshipRequestResponseDto> GetPendingFriendshipAsync(long otherUserId, bool? isIncoming = null)
    {
        var res = await Client.GetAsync("/api/friendships/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        return list!.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
    }

    private async Task<long> SendFriendRequestAndGetOutgoingIdAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester);
        await SendFriendRequestAsync(addressee.Id);
        return (await GetPendingFriendshipAsync(addressee.Id, isIncoming: false)).Id;
    }

    private async Task AcceptFriendshipAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester);
        await SendFriendRequestAsync(addressee.Id);
        await LoginAs(addressee);
        var id = (await GetPendingFriendshipAsync(requester.Id, isIncoming: true)).Id;
        var accept = await Client.PostAsync($"/api/friendships/{id}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    // --- CREATE ---

    [Fact]
    public async Task SendRequest_Returns201_AndPendingFriendship()
    {
        const string testMethodName = "FriendSend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var pending = await GetPendingFriendshipAsync(addressee.Id, isIncoming: false);
        Assert.Equal(FriendshipStatus.Pending, pending.Status);
        Assert.Equal(addressee.Id, pending.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns401_WhenNotAuthenticated()
    {
        const string testMethodName = "FriendSendUnauth";
        var addressee = await CreateUserForTest(testMethodName, 1);
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeMissing()
    {
        const string testMethodName = "FriendSendMissing";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = 999999 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeIsSelf()
    {
        const string testMethodName = "FriendSendSelf";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns409_WhenAlreadyExists()
    {
        const string testMethodName = "FriendSendDup";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await LoginAs(a);
        var first = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = b.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var dup = await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = b.Id });

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // --- GET ---

    [Fact]
    public async Task GetMyFriends_ReturnsAccepted()
    {
        const string testMethodName = "FriendList";
        var me = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var pendingUser = await CreateUserForTest(testMethodName, 3);

        var acceptedId = await SendFriendRequestAndGetOutgoingIdAsync(me, friend);
        await LoginAs(me);
        await SendFriendRequestAsync(pendingUser.Id);

        await LoginAs(friend);
        await Client.PostAsync($"/api/friendships/{acceptedId}/accept", content: null);

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
        await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = outgoing.Id });
        await LoginAs(incoming);
        await Client.PostAsJsonAsync("/api/friendships", new FriendRequestCreateRequestDto { AddresseeId = me.Id });

        await LoginAs(me);
        var res = await Client.GetAsync("/api/friendships/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(list, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsPublic()
    {
        const string testMethodName = "FriendUserListPublic";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Public);

        await LoginAs(stranger);
        var res = await Client.GetAsync($"/api/friendships/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        Assert.Equal(friend.Id, Assert.Single(list!).OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        const string testMethodName = "FriendUserListFriendsOnly";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(stranger);
        var res = await Client.GetAsync($"/api/friendships/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        const string testMethodName = "FriendUserListFriendViewer";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var other = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await AcceptFriendshipAsync(owner, other);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(friend);
        var res = await Client.GetAsync($"/api/friendships/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipRequestResponseDto>>();
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsPrivate()
    {
        const string testMethodName = "FriendUserListPrivate";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Private);

        await LoginAs(friend);
        var res = await Client.GetAsync($"/api/friendships/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- PATCH ---

    [Fact]
    public async Task Accept_Returns200_AndTransitionsToAccepted()
    {
        const string testMethodName = "FriendAccept";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var friendshipId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var res = await Client.PostAsync($"/api/friendships/{friendshipId}/accept", content: null);

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
        var friendshipId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        var res = await Client.PostAsync($"/api/friendships/{friendshipId}/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Reject_Returns200()
    {
        const string testMethodName = "FriendReject";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var friendshipId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var res = await Client.PostAsync($"/api/friendships/{friendshipId}/reject", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<FriendshipRequestResponseDto>();
        Assert.Equal(FriendshipStatus.Rejected, body!.Status);
    }

    // --- DELETE ---

    [Fact]
    public async Task Remove_Returns204_AndDeletesFriendship()
    {
        const string testMethodName = "FriendRemove";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        var friendshipId = await SendFriendRequestAndGetOutgoingIdAsync(a, b);

        var res = await Client.DeleteAsync($"/api/friendships/{friendshipId}");
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
        var friendshipId = await SendFriendRequestAndGetOutgoingIdAsync(a, b);

        await LoginAs(stranger);
        var res = await Client.DeleteAsync($"/api/friendships/{friendshipId}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
