using Location.Tests.Infrastructure;

namespace Location.Tests.Controllers;

public abstract class LocationIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    protected TestUser CreateUserForTest(string testMethodName, long index = 1)
    {
        var user = MonolithAccess.CreateUser(LocationTestAuthHelpers.BuildNickname(testMethodName, index));
        return user;
    }

    protected void LoginAs(TestUser user) => LocationTestAuthHelpers.LoginAs(Client, user.Id);

    protected long CreateGroupWithOwner(TestUser owner)
    {
        var groupId = MonolithAccess.CreateGroup();
        MonolithAccess.AddGroupMember(groupId, owner.Id);
        return groupId;
    }

    protected void AddGroupMember(long groupId, TestUser member) =>
        MonolithAccess.AddGroupMember(groupId, member.Id);
}
