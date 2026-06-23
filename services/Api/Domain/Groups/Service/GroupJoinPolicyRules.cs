using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Service
{
    internal static class GroupJoinPolicyRules
    {
        public static void EnsureCanApply(GroupJoinPolicy policy)
        {
            if (policy == GroupJoinPolicy.Open) throw new ArgumentException("This group allows open join. Use the join endpoint instead.");
            if (policy == GroupJoinPolicy.InvitationOnly) throw new ArgumentException("This group only accepts members by invitation.");
        }

        public static void EnsureCanOpenJoin(GroupJoinPolicy policy)
        {
            if (policy == GroupJoinPolicy.Requestable) throw new ArgumentException("This group requires an application to join.");
            if (policy == GroupJoinPolicy.InvitationOnly) throw new ArgumentException("This group only accepts members by invitation.");
        }
    }
}
