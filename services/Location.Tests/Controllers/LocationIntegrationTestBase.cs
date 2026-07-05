using Location.Tests.Infrastructure;

namespace Location.Tests.Controllers;

public abstract class LocationIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    protected TestUser CreateUserForTest(string testMethodName, long index = 1)
    {
        var user = InMemoryUser.CreateUser($"{testMethodName}User{index}");
        return user;
    }

    protected void LoginAs(TestUser user) => GatewayTestAuthHelpers.LoginAs(Client, user.Id);

    protected long CreateGroupWithOwner(TestUser owner)
    {
        var groupId = FakeGroup.CreateGroup();
        FakeGroup.AddGroupMember(groupId, owner.Id);
        return groupId;
    }

    protected void AddGroupMember(long groupId, TestUser member) =>
        FakeGroup.AddGroupMember(groupId, member.Id);
}
