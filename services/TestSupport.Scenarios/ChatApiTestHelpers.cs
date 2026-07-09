using System.Net;
using System.Net.Http.Json;
using Chat.Dto;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;

namespace Tangle.TestSupport.Scenarios;

public static class ChatApiTestHelpers
{
    public const string ChatRoomsBase = ChatScenarioRequests.ChatRoomsBase;

    public static async Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(
        HttpClient client,
        long asUserId,
        long otherUserId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, asUserId, TestContext.Current.CancellationToken);
        var res = await ChatScenarioRequests.PostDirectRoomAsync(client, otherUserId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(
        HttpClient client,
        long creatorUserId,
        IReadOnlyList<long> otherParticipantIds,
        ITestAuth auth,
        string? title = null)
    {
        await auth.AuthenticateAsync(client, creatorUserId, TestContext.Current.CancellationToken);
        var res = await ChatScenarioRequests.PostMultiRoomAsync(client, otherParticipantIds, title);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<ChatRoomGetResponseDto> CreatePlatformGroupChatRoomAsync(
        HttpClient client,
        long creatorUserId,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        ITestAuth auth,
        string? title = null)
    {
        await auth.AuthenticateAsync(client, creatorUserId, TestContext.Current.CancellationToken);
        var res = await ChatScenarioRequests.PostPlatformGroupChatRoomAsync(
            client, platformGroupId, otherParticipantIds, title);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static Task<HttpResponseMessage> PostMessageAsync(HttpClient client, long roomId, string body) =>
        ChatScenarioRequests.PostMessageAsync(client, roomId, body);
}
