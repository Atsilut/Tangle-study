namespace Api.Domain.Groups.Dto
{
    public record GroupApplicationResponseDto(
        long Id,
        long GroupId,
        long ApplicantId,
        string ApplicantNickname,
        bool IsPending,
        bool IsIncoming,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
