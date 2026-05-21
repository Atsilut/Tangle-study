using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Dto
{
    public record FriendshipRequestResponseDto(
        long Id,
        long RequesterId,
        long AddresseeId,
        long OtherUserId,
        string OtherUserNickname,
        FriendshipStatus Status,
        bool IsIncoming,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
