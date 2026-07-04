namespace Group.Dto
{
    public record GroupBlacklistGetResponseDto(
        long Id,
        long GroupId,
        long UserId,
        string UserNickname,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
