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
    AcceptAsNonInvitee,
    Reject,
    Ignore,
    Cancel,
    CancelAsNonInviterAdmin,
}

public enum ApplicationRequestAction
{
    Approve,
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
