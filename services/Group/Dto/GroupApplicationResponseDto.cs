namespace Group.Dto
{
    public record GroupApplicationGetResponseDto(
        long Id,
        long GroupId,
        string GroupName,
        long ApplicantId,
        string ApplicantNickname,
        bool IsPending,
        bool IsIncoming,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
