namespace Api.Domain.Friendships.Dto
{
    public record FriendRequestGetResponseDto(
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
