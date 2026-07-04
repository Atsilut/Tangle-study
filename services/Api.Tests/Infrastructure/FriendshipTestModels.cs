namespace Api.Tests.Infrastructure;

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
