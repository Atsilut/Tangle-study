using System.Net;
using System.Net.Http.Json;
using Chat.Dto;
using Chat.Tests.Infrastructure;

namespace Chat.Tests.Controllers;

public abstract class ChatIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    protected const string ChatRoomsBase = "/api/chat/rooms";

    protected TestUser CreateUserForTest(string testMethodName, long index = 1) =>
        InMemoryUser.CreateUser(ChatTestAuthHelpers.BuildNickname(testMethodName, index));

    protected void LoginAs(TestUser user) => ChatTestAuthHelpers.LoginAs(Client, user.Id);

    protected void AcceptFriendship(TestUser requester, TestUser addressee) =>
        InMemoryUser.AddFriendship(requester.Id, addressee.Id);

    protected async Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(
        TestUser asUser,
        long otherUserId)
    {
        LoginAs(asUser);
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = otherUserId },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(
        TestUser creator,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null)
    {
        LoginAs(creator);
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/multi",
            new ChatRoomMultiCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<ChatRoomGetResponseDto> CreatePlatformGroupChatRoomAsync(
        TestUser creator,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null)
    {
        LoginAs(creator);
        var res = await Client.PostAsJsonAsync(
            $"/api/groups/{platformGroupId}/chat-rooms",
            new ChatRoomPlatformGroupCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

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
        var res = await Client.GetAsync(ChatRoomsBase, TestContext.Current.CancellationToken);
        if (res.StatusCode == HttpStatusCode.NoContent) return null;
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<ChatRoomSummaryGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<ChatRoomGetResponseDto> GetRoomAsync(long roomId)
    {
        var res = await Client.GetAsync($"{ChatRoomsBase}/{roomId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    protected Task<HttpResponseMessage> LeaveRoomAsync(long roomId) =>
        Client.DeleteAsync($"{ChatRoomsBase}/{roomId}/participants/me", TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> PostMessageAsync(long roomId, string body) =>
        Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages",
            new ChatMessageCreateRequestDto { Body = body },
            TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> PatchMessageAsync(long roomId, long messageId, string body) =>
        Client.PatchAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages/{messageId}",
            new ChatMessagePatchRequestDto { Body = body },
            TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> DeleteMessageAsync(long roomId, long messageId) =>
        Client.DeleteAsync($"{ChatRoomsBase}/{roomId}/messages/{messageId}", TestContext.Current.CancellationToken);

    protected Task<HttpResponseMessage> MarkMessagesSeenAsync(long roomId, params long[] messageIds) =>
        Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages/seen",
            new ChatMessageMarkSeenRequestDto { MessageIds = messageIds },
            TestContext.Current.CancellationToken);

    protected async Task<List<ChatMessageGetResponseDto>> ListMessagesAsync(long roomId)
    {
        var res = await Client.GetAsync($"{ChatRoomsBase}/{roomId}/messages", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }
}
