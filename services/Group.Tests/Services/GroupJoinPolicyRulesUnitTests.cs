using Group.Entities;
using Group.Service;

namespace Group.Tests.Services;

public sealed class GroupJoinPolicyRulesUnitTests
{
    [Fact]
    public void EnsureCanApply_Throws_WhenOpen()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            GroupJoinPolicyRules.EnsureCanApply(GroupJoinPolicy.Open));
        Assert.Contains("open join", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanOpenJoin_Throws_WhenRequestable()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            GroupJoinPolicyRules.EnsureCanOpenJoin(GroupJoinPolicy.Requestable));
        Assert.Contains("application", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanApply_AllowsRequestable()
    {
        GroupJoinPolicyRules.EnsureCanApply(GroupJoinPolicy.Requestable);
    }
}
