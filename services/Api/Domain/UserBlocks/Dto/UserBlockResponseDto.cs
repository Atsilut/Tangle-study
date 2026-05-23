namespace Api.Domain.UserBlocks.Dto
{
    public record UserBlockResponseDto(
        long Id,
        long BlockedUserId,
        string BlockedUserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
