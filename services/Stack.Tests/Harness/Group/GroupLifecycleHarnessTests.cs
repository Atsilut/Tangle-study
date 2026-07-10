using Group.Entities;
using Stack.Tests.Infrastructure;
using System.Net;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Group;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Group)]
public sealed class GroupLifecycleHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task CreateGroup_InviteAndAccept_AddsMember()
    {
        const string testMethodName = nameof(CreateGroup_InviteAndAccept_AddsMember);

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var invitee = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);

        var group = await GroupHarnessHelpers.CreateGroupAsync(
            Client,
            owner,
            GroupVisibility.Public,
            GroupJoinPolicy.InvitationOnly);

        var invitation = await GroupHarnessHelpers.InviteUserAsync(Client, owner, group.Id, invitee.Id);
        await GroupHarnessHelpers.AcceptInvitationAsync(Client, invitee, invitation.Id);

        var members = await GroupHarnessHelpers.GetMembersAsync(Client, owner, group.Id);
        Assert.Contains(members, m => m.UserId == invitee.Id);
        Assert.Contains(members, m => m.UserId == owner.Id && m.Role == GroupRole.Owner);
    }

    [Fact]
    public async Task Invite_Returns400_WhenInviterBlockedInvitee_ViaRealSocial()
    {
        const string testMethodName = nameof(Invite_Returns400_WhenInviterBlockedInvitee_ViaRealSocial);

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var stranger = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);

        var group = await GroupHarnessHelpers.CreateGroupAsync(
            Client,
            owner,
            GroupVisibility.Public,
            GroupJoinPolicy.InvitationOnly);

        await SocialHarnessHelpers.BlockUserAsync(Client, owner, stranger.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, owner);
        var invite = await GroupScenarioRequests.PostInviteUserAsync(Client, group.Id, stranger.Id);

        await IntegrationAssertions.AssertStatusAsync(invite, HttpStatusCode.BadRequest);
    }
}
