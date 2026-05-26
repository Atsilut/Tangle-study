namespace Api.Domain.Groups.Dto
{
    public record GroupInvitationCreateResponseDto(
        long Id,
        long GroupId,
        string GroupName,
        long InviterId,
        long InviteeId,
        long OtherUserId,
        string OtherUserNickname,
        bool IsPending,
        bool IsIncoming,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
