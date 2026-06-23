namespace Api.Domain.UserBlocks.Dto
{
    public record UserBlockGetResponseDto(
        long Id,
        long BlockedUserId,
        string BlockedUserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
