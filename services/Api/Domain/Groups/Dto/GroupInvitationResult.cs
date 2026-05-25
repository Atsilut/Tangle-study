namespace Api.Domain.Groups.Dto
{
    public enum GroupInvitationOutcome
    {
        GroupInvitationCreated,
        GroupMembershipCreatedFromReciprocalApplication,
    }

    public record GroupInvitationResult(GroupInvitationOutcome Outcome, GroupInvitationCreateResponseDto? Invitation);
}
