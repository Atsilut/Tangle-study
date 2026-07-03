using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Domain;
using Api.Global.Dto;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class InternalAccessChatIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private const string InternalSecret = "test-internal-service-secret";

    [Fact]
    public async Task ValidateUserExists_Returns204_WhenUserExists()
    {
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(ValidateUserExists_Returns204_WhenUserExists));

        using var request = CreateInternalRequest(HttpMethod.Post, $"internal/access/users/{user.Id}/validate");
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetNicknames_ReturnsRegisteredNicknames()
    {
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(GetNicknames_ReturnsRegisteredNicknames));

        using var request = CreateInternalRequest(
            HttpMethod.Post,
            "internal/access/users/nicknames",
            new InternalAccessUserIdsRequestDto([user.Id]));
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccessNicknamesResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal(user.Nickname, payload!.Nicknames.Single(entry => entry.UserId == user.Id).Nickname);
    }

    [Fact]
    public async Task ValidateFriendshipPair_Returns400_WhenUsersAreNotFriends()
    {
        var userA = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(ValidateFriendshipPair_Returns400_WhenUsersAreNotFriends), index: 1);
        var userB = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(ValidateFriendshipPair_Returns400_WhenUsersAreNotFriends), index: 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, userA);

        using var request = CreateInternalRequest(
            HttpMethod.Post,
            "internal/access/friendships/validate-pair",
            new InternalAccessOtherUserRequestDto(userB.Id));
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains("friends", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateGroupMember_Returns204_WhenUserIsMember()
    {
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(ValidateGroupMember_Returns204_WhenUserIsMember), index: 1);
        var member = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, nameof(ValidateGroupMember_Returns204_WhenUserIsMember), index: 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);

        var createGroup = await Client.PostAsJsonAsync(
            "/api/groups",
            new GroupCreateRequestDto
            {
                Name = "chat-access-group",
                Description = "d",
                Visibility = GroupVisibility.Private,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createGroup, HttpStatusCode.Created);
        var group = await createGroup.Content.ReadFromJsonAsync<GroupGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(group);

        var invite = await Client.PostAsJsonAsync(
            $"/api/groups/{group!.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = member.Id },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(invite, HttpStatusCode.Created);
        var invitation = await invite.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(invitation);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, member);
        var accept = await Client.PostAsync(
            $"/api/invitations/{invitation!.Id}/accept",
            content: null,
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(accept, HttpStatusCode.OK);

        using var request = CreateInternalRequest(
            HttpMethod.Post,
            $"internal/access/groups/{group.Id}/members/{member.Id}/validate");
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InternalAccess_RequiresInternalSecret()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/access/users/1/validate");
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpRequestMessage CreateInternalRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Internal-Secret", InternalSecret);

        if (Client.DefaultRequestHeaders.Authorization is not null)
            request.Headers.Authorization = Client.DefaultRequestHeaders.Authorization;

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return request;
    }
}
