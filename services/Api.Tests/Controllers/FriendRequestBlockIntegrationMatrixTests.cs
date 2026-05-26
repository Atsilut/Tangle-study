using System.Net;
using Api.Domain.Friendships.Service;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FriendRequestBlockIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    public static TheoryData<FriendshipSetupStep[], FriendshipSetupStep, ExpectedFriendRequestAfterBlock>
        BlockFriendRequestEffectMatrixData =>
        new()
        {
            { [FriendshipSetupStep.SendAtoB], FriendshipSetupStep.UserABlocksB, ExpectedFriendRequestAfterBlock.Deleted },
            { [FriendshipSetupStep.SendAtoB], FriendshipSetupStep.UserBBlocksA, ExpectedFriendRequestAfterBlock.IgnoredByBlock },
            { [FriendshipSetupStep.SendAtoB, FriendshipSetupStep.IgnoreByB], FriendshipSetupStep.UserABlocksB, ExpectedFriendRequestAfterBlock.Deleted },
            { [FriendshipSetupStep.SendAtoB, FriendshipSetupStep.IgnoreByB], FriendshipSetupStep.UserBBlocksA, ExpectedFriendRequestAfterBlock.NotChangedFromIgnored },
        };

    [Theory]
    [MemberData(nameof(BlockFriendRequestEffectMatrixData))]
    public async Task BlockUser_FriendRequestEffect_Matrix(
        FriendshipSetupStep[] setupBeforeBlock,
        FriendshipSetupStep blockAct,
        ExpectedFriendRequestAfterBlock expectedAfterBlock)
    {
        const string prefix = "BlkMatrix";
        var userA = await CreateUserForTest(prefix + "A", 1);
        var userB = await CreateUserForTest(prefix + "B", 2);
        await ApplyFriendshipSetupStepsAsync(userA, userB, setupBeforeBlock);

        if (blockAct == FriendshipSetupStep.UserABlocksB)
        {
            await LoginAs(userA);
            await BlockUserAsync(userB.Id);
        }
        else
        {
            await LoginAs(userB);
            await BlockUserAsync(userA.Id);
        }

        switch (expectedAfterBlock)
        {
            case ExpectedFriendRequestAfterBlock.Deleted:
                await AssertNoPendingBetweenAsync(userA, userB);
                break;
            case ExpectedFriendRequestAfterBlock.IgnoredByBlock:
            case ExpectedFriendRequestAfterBlock.NotChangedFromIgnored:
                await AssertStoredFriendRequestIsPendingAsync(userA.Id, userB.Id, isPending: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedAfterBlock), expectedAfterBlock, null);
        }
    }

    public static TheoryData<FriendshipSetupStep[], HttpStatusCode, bool> ReciprocalSendMatrixData =>
        new()
        {
            { [FriendshipSetupStep.SendAtoB], HttpStatusCode.OK, true },
            { [FriendshipSetupStep.SendAtoB, FriendshipSetupStep.IgnoreByB], HttpStatusCode.OK, true },
        };

    [Theory]
    [MemberData(nameof(ReciprocalSendMatrixData))]
    public async Task Send_ReciprocalFromB_ReturnsOkAndCreatesFriendship(
        FriendshipSetupStep[] setup,
        HttpStatusCode expectedStatus,
        bool expectFriendship)
    {
        const string prefix = "RecipMatrix";
        var userA = await CreateUserForTest(prefix + "A", 1);
        var userB = await CreateUserForTest(prefix + "B", 2);
        await ApplyFriendshipSetupStepsAsync(userA, userB, setup);

        var status = await SendFriendRequestStatusAsync(userB, userA);
        Assert.Equal(expectedStatus, status);
        await AssertFriendshipExistsAsync(userA, userB, expectFriendship);
    }

    [Fact]
    public async Task Send_WhenIgnoredAndBlockedByAddressee_Returns400()
    {
        const string prefix = "RecipBlock";
        var userA = await CreateUserForTest(prefix + "A", 1);
        var userB = await CreateUserForTest(prefix + "B", 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        await IgnoreIncomingRequestAsync(userB, requestId);
        await LoginAs(userB);
        await BlockUserAsync(userA.Id);

        var status = await SendFriendRequestStatusAsync(userB, userA);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        await AssertFriendshipExistsAsync(userA, userB, expected: false);
    }

    [Fact]
    public async Task Send_WhenAlreadyFriends_Returns409()
    {
        const string prefix = "Friends";
        var userA = await CreateUserForTest(prefix + "A", 1);
        var userB = await CreateUserForTest(prefix + "B", 2);
        await AcceptFriendshipAsync(userA, userB);

        var status = await SendFriendRequestStatusAsync(userA, userB);
        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task GetPending_RequesterSeesMaskedPending_WhenOutgoingIgnored()
    {
        const string prefix = "Mask";
        var userA = await CreateUserForTest(prefix + "A", 1);
        var userB = await CreateUserForTest(prefix + "B", 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        await IgnoreIncomingRequestAsync(userB, requestId);

        await AssertPendingDtoAppearsAsync(userA, userB.Id, appearsPending: true, isIncoming: false);
        await LoginAs(userB);
        var res = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
