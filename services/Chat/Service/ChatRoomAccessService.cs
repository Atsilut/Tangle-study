using Chat.Client;
using Chat.Entities;
using Chat.Exceptions;
using Chat.Infrastructure;

namespace Chat.Service;

[Service]
public class ChatRoomAccessService(
    IMonolithAccessClient monolithAccess,
    ISocialClient socialClient,
    IGroupClient groupClient)
{
    private readonly IMonolithAccessClient _monolithAccess = monolithAccess;
    private readonly ISocialClient _socialClient = socialClient;
    private readonly IGroupClient _groupClient = groupClient;

    public Task EnsureNoBlockBetweenUsersAsync(long userId, long otherUserId) =>
        _socialClient.EnsureNoBlockBetweenUsersAsync(userId, otherUserId);

    public async Task EnsureCanCreateDirectRoomAsync(long userId, long otherUserId)
    {
        if (userId == otherUserId) throw new ArgumentException("Cannot create a direct chat room with yourself.");

        await _monolithAccess.EnsureUserExistsAsync(otherUserId);
        await _socialClient.EnsureFriendshipExistsForUserPairAsync(userId, otherUserId);
        await EnsureNoBlockBetweenUsersAsync(userId, otherUserId);
    }

    public void EnsureParticipantInRoom(IReadOnlyList<ChatRoomParticipant> participants, long userId)
    {
        if (!participants.Any(p => p.UserId == userId)) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public void EnsureRoomOwner(IReadOnlyList<ChatRoomParticipant> participants, long userId)
    {
        var participant = participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new EntityNotFoundException("Chat room not found");
        if (!participant.IsOwner) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public void EnsureCanAddParticipant(ChatRoom room, IReadOnlyList<ChatRoomParticipant> participants, long actorUserId)
    {
        if (room.HasRoomOwner) EnsureRoomOwner(participants, actorUserId);
        else EnsureParticipantInRoom(participants, actorUserId);
    }

    public async Task EnsureInviteeCanBeAddedAsync(
        ChatRoom room,
        long inviteeUserId,
        IReadOnlyList<ChatRoomParticipant> participants)
    {
        await _monolithAccess.EnsureUserExistsAsync(inviteeUserId);

        await _socialClient.EnsureNoBlockBetweenUserAndOthersAsync(
            inviteeUserId,
            [.. participants.Select(p => p.UserId)]);

        if (room.Kind == ChatRoomKind.PlatformGroup)
        {
            var platformGroupId = room.PlatformGroupId
                ?? throw new InvalidOperationException("Platform group room is missing PlatformGroupId.");
            await _groupClient.EnsureGroupMemberAsync(
                platformGroupId,
                inviteeUserId,
                "User is not a member of this group");
        }
    }

    public async Task EnsureGroupMemberCanListRoomsAsync(long platformGroupId, long userId)
    {
        await _groupClient.EnsureGroupExistsAsync(platformGroupId);
        await _groupClient.EnsureGroupMemberAsync(platformGroupId, userId, "Group not found");
    }

    public async Task EnsureCanCreatePlatformGroupRoomAsync(
        long platformGroupId,
        long creatorUserId,
        IReadOnlyCollection<long> participantUserIds)
    {
        await _groupClient.EnsureGroupExistsAsync(platformGroupId);
        await _groupClient.EnsureGroupMemberAsync(platformGroupId, creatorUserId, "Group not found");

        var otherParticipantIds = OtherParticipantIds(creatorUserId, participantUserIds);
        if (otherParticipantIds.Count == 0) return;

        await _monolithAccess.EnsureUsersExistAsync(otherParticipantIds);
        await _socialClient.EnsureNoBlockBetweenUserAndOthersAsync(creatorUserId, otherParticipantIds);
        await _groupClient.EnsureGroupMembersAsync(
            platformGroupId,
            otherParticipantIds,
            "All participants must be members of this group");
    }

    public async Task EnsureCanCreateMultiRoomAsync(long creatorUserId, IReadOnlyCollection<long> participantUserIds)
    {
        var otherParticipantIds = OtherParticipantIds(creatorUserId, participantUserIds);
        if (otherParticipantIds.Count == 0) return;

        await _monolithAccess.EnsureUsersExistAsync(otherParticipantIds);
        await _socialClient.EnsureNoBlockBetweenUserAndOthersAsync(creatorUserId, otherParticipantIds);
    }

    private static List<long> OtherParticipantIds(long creatorUserId, IReadOnlyCollection<long> participantUserIds) =>
        [.. participantUserIds.Where(id => id != creatorUserId).Distinct()];
}
