namespace Api.Domain.Friendships.Dto
{
    public record FriendRequestResponseDto(
        long Id,
        long RequesterId,
        long AddresseeId,
        long OtherUserId,
        string OtherUserNickname,
        bool IsPending,
        bool IsIncoming,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
