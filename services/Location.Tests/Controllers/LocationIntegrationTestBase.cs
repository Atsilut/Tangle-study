using Location.Tests.Infrastructure;

namespace Location.Tests.Controllers;

public abstract class LocationIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    protected TestUser CreateUserForTest(string testMethodName, long index = 1)
    {
        var user = InMemoryUser.CreateUser(LocationTestAuthHelpers.BuildNickname(testMethodName, index));
        return user;
    }

    protected void LoginAs(TestUser user) => LocationTestAuthHelpers.LoginAs(Client, user.Id);

    protected long CreateGroupWithOwner(TestUser owner)
    {
        var groupId = InMemoryUser.CreateGroup();
        InMemoryUser.AddGroupMember(groupId, owner.Id);
        return groupId;
    }

    protected void AddGroupMember(long groupId, TestUser member) =>
        InMemoryUser.AddGroupMember(groupId, member.Id);
}
