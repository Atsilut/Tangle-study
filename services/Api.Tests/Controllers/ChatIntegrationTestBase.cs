using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public abstract class ChatIntegrationTestBase(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    protected const string ChatRoomsBase = "/api/chat/rooms";

    protected async Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(
        UserGetResponseDto asUser,
        long otherUserId)
    {
        await LoginAs(asUser);
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = otherUserId });
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>())!;
    }

    protected async Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(
        UserGetResponseDto creator,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null)
    {
        await LoginAs(creator);
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/multi",
            new ChatRoomMultiCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            });
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>())!;
    }

    protected async Task<ChatRoomGetResponseDto> CreatePlatformGroupChatRoomAsync(
        UserGetResponseDto creator,
        long platformGroupId,
        IReadOnlyList<long> otherParticipantIds,
        string? title = null)
    {
        await LoginAs(creator);
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{platformGroupId}/chat-rooms",
            new ChatRoomPlatformGroupCreateRequestDto
            {
                Title = title,
                ParticipantUserIds = otherParticipantIds,
            });
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>())!;
    }

    protected async Task<GroupResponseDto> CreateGroupWithMemberAsync(
        UserGetResponseDto owner,
        UserGetResponseDto member)
    {
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        return group;
    }

    protected async Task<List<ChatRoomSummaryGetResponseDto>?> ListMyRoomsAsync()
    {
        var res = await Client.GetAsync(ChatRoomsBase);
        if (res.StatusCode == HttpStatusCode.NoContent) return null;
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<ChatRoomSummaryGetResponseDto>>())!;
    }

    protected async Task<ChatRoomGetResponseDto> GetRoomAsync(long roomId)
    {
        var res = await Client.GetAsync($"{ChatRoomsBase}/{roomId}");
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>())!;
    }

    protected Task<HttpResponseMessage> LeaveRoomAsync(long roomId) =>
        Client.DeleteAsync($"{ChatRoomsBase}/{roomId}/participants/me");

    protected Task<HttpResponseMessage> PostMessageAsync(long roomId, string body) =>
        Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{roomId}/messages",
            new ChatMessageCreateRequestDto { Body = body });
}
