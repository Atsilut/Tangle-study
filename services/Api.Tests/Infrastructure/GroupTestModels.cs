namespace Api.Tests.Infrastructure;

public enum GroupActorRole
{
    Anonymous,
    Owner,
    Admin,
    Member,
    Stranger,
}

public enum GroupTargetRole
{
    Owner,
    Admin,
    OtherAdmin,
    Member,
    OtherMember,
    Self,
}

public enum GroupReadOperation
{
    GetGroup,
    GetMembers,
}

public enum GroupManagementAction
{
    Update,
    Delete,
    TransferToMember,
    TransferToSelf,
    TransferToStranger,
}

public enum GroupExpectedOutcome
{
    Ok,
    NotFound,
    Unauthorized,
    ArgumentException,
}

public enum JoinPolicyOperation
{
    Join,
    Apply,
}

public enum JoinPolicyRouteOutcome
{
    MemberAdded,
    UseJoinEndpoint,
    RequiresApplication,
    InvitationOnly,
    ApplicationCreated,
}

public enum BlacklistAdminAction
{
    Add,
    Remove,
}

public enum InvitationRequestAction
{
    Accept,
    /// <summary>Non-invitee attempts accept (same HTTP route as <see cref="Accept"/>).</summary>
    AcceptAsNonInvitee,
    Reject,
    Ignore,
    Cancel,
    /// <summary>Group admin/owner who did not send the invite cancels (same HTTP route as <see cref="Cancel"/>).</summary>
    CancelAsNonInviterAdmin,
}

public enum ApplicationRequestAction
{
    Approve,
    /// <summary>Applicant attempts approve (same HTTP route as <see cref="Approve"/>).</summary>
    ApproveAsApplicant,
    Reject,
    Ignore,
    Cancel,
}

public enum BoardCrudOperation
{
    Create,
    Update,
    Delete,
}

public enum FriendshipSetupStep
{
    SendAtoB,
    IgnoreByB,
    UserBBlocksA,
    UserABlocksB,
}

public enum ExpectedFriendRequestAfterBlock
{
    Deleted,
    IgnoredByBlock,
    NotChangedFromIgnored,
}
