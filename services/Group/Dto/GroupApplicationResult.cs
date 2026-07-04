namespace Group.Dto
{
    public enum GroupApplicationOutcome
    {
        GroupApplicationCreated,
        GroupMembershipCreatedFromReciprocalInvitation,
    }

    public record GroupApplicationResult(GroupApplicationOutcome Outcome, GroupApplicationGetResponseDto? Application);
}
