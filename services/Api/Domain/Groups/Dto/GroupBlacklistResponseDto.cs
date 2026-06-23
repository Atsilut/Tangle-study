namespace Api.Domain.Groups.Dto
{
    public record GroupBlacklistGetResponseDto(
        long Id,
        long GroupId,
        long UserId,
        string UserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
