namespace Api.Domain.Groups.Dto
{
    public record GroupInvitationCreateResponseDto(
        long Id,
        long GroupId,
        string GroupName,
        long InviterId,
        long InviteeId,
        bool IsPending,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
