using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Controllers;

internal static class GroupIntegrationTestHelpers
{
    public const string DefaultPassword = "testtest123!";
    public const string GroupsBase = "/api/groups";

    public static async Task<UserGetResponseDto> CreateUserForTestAsync(
        HttpClient client,
        string testMethodName,
        long index = 1,
        string? password = null)
    {
        password ??= DefaultPassword;
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = $"{testMethodName}User" + index.ToString();
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    public static async Task LoginAsAsync(HttpClient client, UserGetResponseDto user, string? password = null)
    {
        password ??= DefaultPassword;
        var req = new LoginRequestDto
        {
            Email = user.Email,
            Password = password,
        };
        var login = await client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var loginRes = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginRes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }

    public static async Task<GroupResponseDto> CreateGroupAsAsync(
        HttpClient client,
        UserGetResponseDto user,
        GroupVisibility visibility = GroupVisibility.Private,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable)
    {
        await LoginAsAsync(client, user);
        var res = await client.PostAsJsonAsync(GroupsBase, new GroupCreateRequestDto
        {
            Name = $"Group_{Guid.NewGuid():N}".Substring(0, 20),
            Description = "test group",
            Visibility = visibility,
            JoinPolicy = joinPolicy,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupResponseDto>())!;
    }

    public static async Task BlockUserAsync(HttpClient client, long blockedUserId)
    {
        var res = await client.PostAsJsonAsync("/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    public static async Task SeedGroupMemberAsync(
        ApiWebApplicationFactory factory,
        long groupId,
        long userId,
        GroupRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GroupMembers.Add(new GroupMember(groupId, userId, role));
        await db.SaveChangesAsync();
    }
}
