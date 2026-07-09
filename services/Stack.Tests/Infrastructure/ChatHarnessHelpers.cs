using Chat.Dto;
using Chat.Queue;
using Microsoft.AspNetCore.SignalR.Client;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Tangle.TestSupport.Scenarios;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

internal static class ChatHarnessHelpers
{
    public const string ChatRoomsBase = ChatScenarioRequests.ChatRoomsBase;
    public const string WorkQueueStreamPrefix = "tangle:queue:";

    private static ITestAuth Auth => HarnessJwtAuth.Instance;

    public static HubConnection BuildHubConnection(HttpClient client) =>
        HarnessHubConnectionFactory.Build(client, "hubs/chat");

    public static string GetWorkQueueStreamKey(string streamName = WorkQueueStreams.ChatMessageCreated) =>
        $"{WorkQueueStreamPrefix}{streamName}";

    public static Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(
        HttpClient client,
        UserGetResponseDto asUser,
        long otherUserId) =>
        ChatApiTestHelpers.GetOrCreateDirectRoomAsync(client, asUser.Id, otherUserId, Auth);

    public static Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(
        HttpClient client,
        UserGetResponseDto creator,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        ChatApiTestHelpers.CreateMultiRoomAsync(client, creator.Id, otherParticipantIds, Auth, title);

    public static Task<ChatRoomGetResponseDto> CreatePlatformGroupChatRoomAsync(
        HttpClient client,
        UserGetResponseDto creator,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null) =>
        ChatApiTestHelpers.CreatePlatformGroupChatRoomAsync(
            client, creator.Id, platformGroupId, otherParticipantIds, Auth, title);

    public static Task<HttpResponseMessage> PostMessageAsync(HttpClient client, long roomId, string body) =>
        ChatScenarioRequests.PostMessageAsync(client, roomId, body);
}
