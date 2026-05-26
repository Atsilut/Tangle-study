namespace Api.Domain.Groups.Dto
{
    public record GroupBlacklistResponseDto(
        long Id,
        long GroupId,
        long UserId,
        string UserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
