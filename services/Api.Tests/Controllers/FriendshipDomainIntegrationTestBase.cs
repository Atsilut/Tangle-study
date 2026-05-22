using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public abstract class FriendshipDomainIntegrationTestBase(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testPassword = "testtest123!";
    protected const string RequestsBase = "/api/friendships/requests";
    protected const string FriendshipsBase = "/api/friendships";

    protected async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, long index = 1, string password = "testtest123!")
    {
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = $"{testMethodName}User" + index.ToString();
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    protected async Task LoginAs(UserGetResponseDto user, string password = "testtest123!")
    {
        var req = new LoginRequestDto { Email = user.Email, Password = password };
        var login = await Client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
    }

    protected async Task SetFriendsListVisibilityAsync(UserGetResponseDto user, FriendsListVisibility visibility)
    {
        await LoginAs(user, testPassword);
        var res = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = visibility });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    protected async Task SendFriendRequestAsync(long addresseeId)
    {
        var res = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    protected async Task<FriendRequestResponseDto> GetPendingRequestAsync(long otherUserId, bool? isIncoming = null)
    {
        var res = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.NotNull(list);
        return list.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
    }

    protected async Task<long> SendFriendRequestAndGetOutgoingIdAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester, testPassword);
        await SendFriendRequestAsync(addressee.Id);
        return (await GetPendingRequestAsync(addressee.Id, isIncoming: false)).Id;
    }

    protected async Task AcceptFriendshipAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester, testPassword);
        await SendFriendRequestAsync(addressee.Id);
        await LoginAs(addressee, testPassword);
        var id = (await GetPendingRequestAsync(requester.Id, isIncoming: true)).Id;
        var accept = await Client.PostAsync($"{RequestsBase}/{id}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    protected async Task<FriendshipResponseDto> GetAcceptedFriendAsync(long otherUserId)
    {
        var res = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.NotNull(list);
        return list.Single(f => f.OtherUserId == otherUserId);
    }
}
