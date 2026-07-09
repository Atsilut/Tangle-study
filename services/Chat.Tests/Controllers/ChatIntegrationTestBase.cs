using System.Net;
using System.Net.Http.Json;
using Chat.Dto;
using Chat.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Chat.Tests.Scenarios;

namespace Chat.Tests.Controllers;

public abstract class ChatIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    protected const string ChatRoomsBase = ChatScenarioRequests.ChatRoomsBase;

    protected static ITestAuth Auth => GatewayHeaderAuth.Instance;

    protected TestUser CreateUserForTest(string testMethodName, long index = 1) =>
        InMemoryUser.CreateUser(TestUserIdentity.BuildNickname(testMethodName, index));

    protected void LoginAs(TestUser user) => GatewayTestAuthHelpers.LoginAs(Client, user.Id);

    protected void AcceptFriendship(TestUser requester, TestUser addressee) =>
        InMemoryUser.AddFriendship(requester.Id, addressee.Id);

    protected Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(
        TestUser asUser,
        long otherUserId) =>
        ChatApiTestHelpers.GetOrCreateDirectRoomAsync(Client, asUser.Id, otherUserId, Auth);

    protected Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(
        TestUser creator,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        ChatApiTestHelpers.CreateMultiRoomAsync(Client, creator.Id, otherParticipantIds, Auth, title);

    protected Task<ChatRoomGetResponseDto> CreatePlatformGroupChatRoomAsync(
        TestUser creator,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        ChatApiTestHelpers.CreatePlatformGroupChatRoomAsync(
            Client, creator.Id, platformGroupId, otherParticipantIds, Auth, title);

    protected (TestUser Owner, TestUser Member, long GroupId) CreateGroupWithMember(
        TestUser owner,
        TestUser member)
    {
        var groupId = InMemoryUser.CreateGroup();
        InMemoryUser.AddGroupMember(groupId, owner.Id);
        InMemoryUser.AddGroupMember(groupId, member.Id);
        return (owner, member, groupId);
    }

    protected void SeedGroupMember(long groupId, TestUser member) =>
        InMemoryUser.AddGroupMember(groupId, member.Id);

    protected async Task<List<ChatRoomSummaryGetResponseDto>?> ListMyRoomsAsync()
    {
        var res = await ChatScenarioRequests.GetRoomsAsync(Client);
        if (res.StatusCode == HttpStatusCode.NoContent) return null;
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<ChatRoomSummaryGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<ChatRoomGetResponseDto> GetRoomAsync(long roomId)
    {
        var res = await ChatScenarioRequests.GetRoomAsync(Client, roomId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    protected Task<HttpResponseMessage> LeaveRoomAsync(long roomId) =>
        ChatScenarioRequests.LeaveRoomAsync(Client, roomId);

    protected Task<HttpResponseMessage> PostMessageAsync(long roomId, string body) =>
        ChatScenarioRequests.PostMessageAsync(Client, roomId, body);

    protected Task<HttpResponseMessage> PatchMessageAsync(long roomId, long messageId, string body) =>
        ChatScenarioRequests.PatchMessageAsync(Client, roomId, messageId, body);

    protected Task<HttpResponseMessage> DeleteMessageAsync(long roomId, long messageId) =>
        ChatScenarioRequests.DeleteMessageAsync(Client, roomId, messageId);

    protected Task<HttpResponseMessage> MarkMessagesSeenAsync(long roomId, params long[] messageIds) =>
        ChatScenarioRequests.MarkMessagesSeenAsync(Client, roomId, messageIds);

    protected async Task<List<ChatMessageGetResponseDto>> ListMessagesAsync(long roomId)
    {
        var res = await ChatScenarioRequests.ListMessagesAsync(Client, roomId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }
}
