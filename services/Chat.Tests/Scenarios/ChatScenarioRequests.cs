using System.Net.Http.Json;
using Chat.Dto;

namespace Chat.Tests.Scenarios;

public static class ChatScenarioRequests
{
    public const string ChatRoomsBase = "/api/chat/rooms";

    public static Task<HttpResponseMessage> PostDirectRoomAsync(HttpClient client, long otherUserId) =>
        client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = otherUserId },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PostMultiRoomAsync(
        HttpClient client,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        client.PostAsJsonAsync(
            $"{ChatRoomsBase}/multi",
            new ChatRoomMultiCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PostPlatformGroupChatRoomAsync(
        HttpClient client,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        client.PostAsJsonAsync(
            $"/api/groups/{platformGroupId}/chat-rooms",
            new ChatRoomPlatformGroupCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetRoomsAsync(HttpClient client) =>
        client.GetAsync(ChatRoomsBase, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetRoomAsync(HttpClient client, long roomId) =>
        client.GetAsync($"{ChatRoomsBase}/{roomId}", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LeaveRoomAsync(HttpClient client, long roomId) =>
        client.DeleteAsync($"{ChatRoomsBase}/{roomId}/participants/me", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PostMessageAsync(HttpClient client, long roomId, string body) =>
        client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages",
            new ChatMessageCreateRequestDto { Body = body },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PatchMessageAsync(HttpClient client, long roomId, long messageId, string body) =>
        client.PatchAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages/{messageId}",
            new ChatMessagePatchRequestDto { Body = body },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> DeleteMessageAsync(HttpClient client, long roomId, long messageId) =>
        client.DeleteAsync($"{ChatRoomsBase}/{roomId}/messages/{messageId}", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> MarkMessagesSeenAsync(
        HttpClient client,
        long roomId,
        params long[] messageIds) =>
        client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages/seen",
            new ChatMessageMarkSeenRequestDto { MessageIds = messageIds },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> ListMessagesAsync(HttpClient client, long roomId) =>
        client.GetAsync($"{ChatRoomsBase}/{roomId}/messages", TestContext.Current.CancellationToken);
}
