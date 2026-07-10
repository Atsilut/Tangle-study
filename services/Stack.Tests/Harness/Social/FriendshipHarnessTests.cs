using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Social;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Social)]
public sealed class FriendshipHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task FriendRequest_CreateAndAccept_ThroughGateway()
    {
        const string testMethodName = nameof(FriendRequest_CreateAndAccept_ThroughGateway);

        var requester = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var addressee = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);

        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, requester, addressee);

        await HarnessAuthHelpers.LoginAsAsync(Client, requester);
        var friendship = await SocialHarnessHelpers.GetAcceptedFriendAsync(Client, addressee.Id);
        Assert.Equal(addressee.Id, friendship.OtherUserId);
    }
}
