using System.Net;
using Social.Tests.Infrastructure;

namespace Social.Tests.Controllers;

/// <summary>
/// Block × friend-request state matrix. Rows cover pending vs ignored requests when A or B blocks.
/// Out of matrix (covered by facts in this class):
/// reciprocal send after pending/ignore (<see cref="Send_ReciprocalFromB_ReturnsOkAndCreatesFriendship"/>),
/// reciprocal send blocked after ignore + block (<see cref="Send_WhenIgnoredAndBlockedByAddressee_Returns400"/>),
/// send when already friends 409 (<see cref="Send_WhenAlreadyFriends_Returns409"/>),
/// pending-list masking for ignored outgoing (<see cref="GetPending_RequesterSeesMaskedPending_WhenOutgoingIgnored"/>).
/// </summary>
[Collection(SocialIntegrationTestCollection.Name)]
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
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        await ApplyFriendshipSetupStepsAsync(userA, userB, setupBeforeBlock);

        if (blockAct == FriendshipSetupStep.UserABlocksB)
        {
            LoginAs(userA);
            await BlockUserAsync(userB);
        }
        else
        {
            LoginAs(userB);
            await BlockUserAsync(userA);
        }

        switch (expectedAfterBlock)
        {
            case ExpectedFriendRequestAfterBlock.Deleted:
                await AssertNoPendingBetweenAsync(userA, userB);
                break;
            case ExpectedFriendRequestAfterBlock.IgnoredByBlock:
            case ExpectedFriendRequestAfterBlock.NotChangedFromIgnored:
                await AssertStoredFriendRequestIsPendingAsync(userA, userB, isPending: false);
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
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        await ApplyFriendshipSetupStepsAsync(userA, userB, setup);

        var status = await SendFriendRequestStatusAsync(userB, userA);

        Assert.Equal(expectedStatus, status);
        await AssertFriendshipExistsAsync(userA, userB, expectFriendship);
    }

    [Fact]
    public async Task Send_WhenIgnoredAndBlockedByAddressee_Returns400()
    {
        const string prefix = "RecipBlock";
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        await IgnoreIncomingRequestAsync(userB, requestId);
        LoginAs(userB);
        await BlockUserAsync(userA);

        var status = await SendFriendRequestStatusAsync(userB, userA);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        await AssertFriendshipExistsAsync(userA, userB, expected: false);
    }

    [Fact]
    public async Task Send_WhenAlreadyFriends_Returns409()
    {
        const string prefix = "Friends";
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        await AcceptFriendshipAsync(userA, userB);

        var status = await SendFriendRequestStatusAsync(userA, userB);

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task GetPending_RequesterSeesMaskedPending_WhenOutgoingIgnored()
    {
        const string prefix = "Mask";
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        await IgnoreIncomingRequestAsync(userB, requestId);
        await AssertPendingDtoAppearsAsync(userA, userB, appearsPending: true, isIncoming: false);
        LoginAs(userB);

        var res = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnblockUser_RestoresPending_WhenAddresseeBlockedRequesterWithPendingRequest()
    {
        const string prefix = "UnblockRestore";
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        LoginAs(userB);
        await BlockUserAsync(userA);
        await AssertStoredFriendRequestIsPendingAsync(userA, userB, isPending: false);

        await UnblockUserAsync(userA);

        await AssertStoredFriendRequestIsPendingAsync(userA, userB, isPending: true);
        await AssertPendingDtoAppearsAsync(userB, userA, appearsPending: true, isIncoming: true);
    }

    [Fact]
    public async Task UnblockUser_KeepsIgnored_WhenManuallyIgnoredBeforeBlock()
    {
        const string prefix = "UnblockManualIgnore";
        var userA = CreateUserForTest(prefix + "A", 1);
        var userB = CreateUserForTest(prefix + "B", 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
        await IgnoreIncomingRequestAsync(userB, requestId);
        LoginAs(userB);
        await BlockUserAsync(userA);
        await AssertStoredFriendRequestIsPendingAsync(userA, userB, isPending: false);

        await UnblockUserAsync(userA);

        await AssertStoredFriendRequestIsPendingAsync(userA, userB, isPending: false);
        var pendingRes = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(pendingRes, HttpStatusCode.NoContent);
    }
}
