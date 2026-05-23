namespace Api.Domain.Friendships.Dto
{
    public record FriendshipResponseDto(
        long Id,
        long OtherUserId,
        string OtherUserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
