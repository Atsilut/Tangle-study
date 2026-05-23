using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendRequestBlockMatrixTests
{
    private static async Task<BlockMatrixScenario> ApplySetupAsync(
        BlockMatrixScenario scenario,
        IReadOnlyList<FriendshipSetupStep> steps)
    {
        long? requestId = null;
        foreach (var step in steps)
        {
            switch (step)
            {
                case FriendshipSetupStep.SendAtoB:
                    requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
                    break;
                case FriendshipSetupStep.IgnoreByB:
                    requestId ??= (await scenario.GetStoredRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id))!.Id;
                    await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId.Value);
                    break;
                case FriendshipSetupStep.UserBBlocksA:
                    await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);
                    break;
                case FriendshipSetupStep.UserABlocksB:
                    await scenario.BlockAsync(scenario.UserA.Id, scenario.UserB.Id);
                    break;
            }
        }

        return scenario;
    }

    // --- Block effect on friend request matrix ---

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
        var scenario = await BlockMatrixScenario.CreateAsync($"blk_{Guid.NewGuid():N}"[..8]);
        await ApplySetupAsync(scenario, setupBeforeBlock);
        await ApplyBlockActAsync(scenario, blockAct);

        switch (expectedAfterBlock)
        {
            case ExpectedFriendRequestAfterBlock.Deleted:
                await scenario.AssertNoRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id);
                break;
            case ExpectedFriendRequestAfterBlock.IgnoredByBlock:
            case ExpectedFriendRequestAfterBlock.NotChangedFromIgnored:
                await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedAfterBlock), expectedAfterBlock, null);
        }
    }

    private static Task ApplyBlockActAsync(BlockMatrixScenario scenario, FriendshipSetupStep blockAct) =>
        blockAct switch
        {
            FriendshipSetupStep.UserABlocksB => scenario.BlockAsync(scenario.UserA.Id, scenario.UserB.Id),
            FriendshipSetupStep.UserBBlocksA => scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(blockAct), blockAct, "Expected a block act step."),
        };

    // --- Send matrix ---

    [Theory]
    [MemberData(nameof(ReciprocalSendMatrixData))]
    public async Task Send_ReciprocalFromB_ReturnsExpectedOutcome(
        FriendshipSetupStep[] setup,
        SendFriendRequestOutcome expectedOutcome,
        bool? storedIsPending,
        bool expectFriendship)
    {
        var scenario = await BlockMatrixScenario.CreateAsync($"recip_{Guid.NewGuid():N}"[..8]);
        await ApplySetupAsync(scenario, setup);

        var outcome = await scenario.SendAsync(scenario.UserB.Id, scenario.UserA.Id);

        Assert.Equal(expectedOutcome, outcome);
        if (storedIsPending.HasValue)
            await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, storedIsPending);
        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expectFriendship);
    }

    public static TheoryData<FriendshipSetupStep[], SendFriendRequestOutcome, bool?, bool> ReciprocalSendMatrixData =>
        new()
        {
            { [FriendshipSetupStep.SendAtoB], SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest, null, true },
            { [FriendshipSetupStep.SendAtoB, FriendshipSetupStep.IgnoreByB], SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest, null, true },
        };

    [Fact]
    public async Task Send_WhenIgnoredAndBlockedByAddressee_ThrowsWhenAddresseeSendsBack()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("recip_block");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.SendAsync(scenario.UserB.Id, scenario.UserA.Id));

        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expected: false);
    }

    [Fact]
    public async Task Send_WhenPendingAndRequesterBlockedAddressee_ReturnsCreatedWithoutFriendship()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("a_blocks_b");
        await scenario.SendAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.BlockAsync(scenario.UserA.Id, scenario.UserB.Id);

        var outcome = await scenario.SendAsync(scenario.UserB.Id, scenario.UserA.Id);

        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        // A→B was deleted when A blocked B; B→A is created ignored because A still blocks B.
        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expected: false);
    }

    [Fact]
    public async Task Send_WhenAlreadyFriends_ThrowsEntityAlreadyExists()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("friends");
        await scenario.AcceptFriendshipAsync(scenario.UserA.Id, scenario.UserB.Id);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.SendAsync(scenario.UserA.Id, scenario.UserB.Id));
    }

    [Fact]
    public async Task Send_WhenIgnoredAndBlocked_AResendReturnsCreatedWithoutReactivate()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("resend");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        var outcome = await scenario.SendAsync(scenario.UserA.Id, scenario.UserB.Id);

        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
        await scenario.AssertPendingDtoForUserAsync(scenario.UserA.Id, scenario.UserB.Id, appearsPending: true);
    }

    [Fact]
    public async Task Send_WhenBlockOnlyBeforeFirstSend_CreatesIgnoredRequest()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("blk_first");
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        var outcome = await scenario.SendAsync(scenario.UserA.Id, scenario.UserB.Id);

        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
        await scenario.AssertPendingDtoForUserAsync(scenario.UserA.Id, scenario.UserB.Id, appearsPending: true);
    }

    // --- Accept / reject / ignore matrix ---

    [Fact]
    public async Task Accept_WhenIgnoredIncomingWithoutBlock_CreatesFriendship()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("accept_ign");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AcceptIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expected: true);
        await scenario.AssertNoRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id);
    }

    [Fact]
    public async Task Accept_WhenIgnoredIncomingAndBlocked_ThrowsArgument()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("accept_blk");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.AcceptIncomingAsync(scenario.UserB.Id, requestId));

        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expected: false);
        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
    }

    [Fact]
    public async Task Reject_WhenIgnoredIncomingWithoutBlock_DeletesRequest()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("reject_ign");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.RejectIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AssertNoRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.AssertFriendshipExistsAsync(scenario.UserA.Id, scenario.UserB.Id, expected: false);
    }

    [Fact]
    public async Task Reject_WhenIgnoredIncomingAndBlocked_DeletesRequest()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("reject_blk");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        await scenario.RejectIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AssertNoRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id);
    }

    [Fact]
    public async Task Ignore_WhenBlockThenIgnoreOnPending_IsIdempotent()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("blk_ign");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        var stored = await scenario.GetStoredRequestForPairAsync(scenario.UserA.Id, scenario.UserB.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
    }

    [Fact]
    public async Task Ignore_WhenIgnoreThenBlockOnIgnored_LeavesIgnoredUnchanged()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("ign_blk");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AssertRequestExistsAsync(scenario.UserA.Id, scenario.UserB.Id, isPending: false);
    }

    // --- Read API matrix ---

    [Fact]
    public async Task GetPending_RequesterSeesMaskedPending_WhenOutgoingIgnored()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("mask");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        await scenario.AssertPendingDtoForUserAsync(scenario.UserA.Id, scenario.UserB.Id, appearsPending: true, isIncoming: false);
        Assert.Null(await scenario.GetPendingForUserAsync(scenario.UserB.Id));
    }

    [Fact]
    public async Task GetIgnoredIncoming_ListsIgnoredIncoming_ForAddressee()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("ign_list");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);

        var ignored = await scenario.GetIgnoredIncomingForUserAsync(scenario.UserB.Id);
        Assert.NotNull(ignored);
        Assert.Contains(ignored, d => d.Id == requestId && !d.IsPending);
    }

    [Fact]
    public async Task GetPending_AddresseeSeesNoIncoming_WhenBlockedAndIgnored()
    {
        var scenario = await BlockMatrixScenario.CreateAsync("no_in");
        var requestId = await scenario.SendAndGetRequestIdAsync(scenario.UserA.Id, scenario.UserB.Id);
        await scenario.IgnoreIncomingAsync(scenario.UserB.Id, requestId);
        await scenario.BlockAsync(scenario.UserB.Id, scenario.UserA.Id);

        Assert.Null(await scenario.GetPendingForUserAsync(scenario.UserB.Id));
    }

    private sealed class BlockMatrixScenario
    {
        private readonly FakeHttpContextAccessor _httpContextAccessor;
        private readonly FriendRequestService _friendRequestService;
        private readonly UserBlockService _userBlockService;
        private readonly FakeFriendRequestRepository _friendRequestRepository;
        private readonly FakeFriendshipRepository _friendshipRepository;
        private readonly FakeUserRepository _userRepository;

        public User UserA { get; private set; } = null!;
        public User UserB { get; private set; } = null!;

        private BlockMatrixScenario(FakeHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            var graph = DomainServiceTestFactory.Create(httpContextAccessor);
            _friendRequestService = graph.FriendRequestService;
            _userBlockService = graph.UserBlockService;
            _friendRequestRepository = graph.FriendRequestRepository;
            _friendshipRepository = graph.FriendshipRepository;
            _userRepository = graph.UserRepository;
        }

        public static async Task<BlockMatrixScenario> CreateAsync(string nicknamePrefix = "user")
        {
            var http = new FakeHttpContextAccessor("1");
            var scenario = new BlockMatrixScenario(http);
            scenario.UserA = await scenario.CreateTestUserAsync($"{nicknamePrefix}A");
            scenario.UserB = await scenario.CreateTestUserAsync($"{nicknamePrefix}B");
            return scenario;
        }

        private async Task<User> CreateTestUserAsync(string nickname)
        {
            var user = new User(
                email: $"{nickname}@test.com",
                password: "password",
                nickname: nickname);
            await _userRepository.CreateUserAsync(user);
            return user;
        }

        public void LoginAs(long userId) =>
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
            };

        public async Task<SendFriendRequestOutcome> SendAsync(long requesterId, long addresseeId)
        {
            LoginAs(requesterId);
            return await _friendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        }

        public async Task<long> SendAndGetRequestIdAsync(long requesterId, long addresseeId)
        {
            await SendAsync(requesterId, addresseeId);
            var pending = await GetPendingForUserAsync(requesterId);
            return pending!.Single(p => p.OtherUserId == addresseeId).Id;
        }

        public async Task IgnoreIncomingAsync(long addresseeId, long requestId)
        {
            LoginAs(addresseeId);
            await _friendRequestService.IgnoreRequestAsync(requestId);
        }

        public async Task AcceptIncomingAsync(long addresseeId, long requestId)
        {
            LoginAs(addresseeId);
            await _friendRequestService.AcceptRequestAsync(requestId);
        }

        public async Task RejectIncomingAsync(long addresseeId, long requestId)
        {
            LoginAs(addresseeId);
            await _friendRequestService.RejectRequestAsync(requestId);
        }

        public async Task BlockAsync(long blockerId, long blockedUserId)
        {
            LoginAs(blockerId);
            await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blockedUserId });
        }

        public async Task AcceptFriendshipAsync(long requesterId, long addresseeId)
        {
            var requestId = await SendAndGetRequestIdAsync(requesterId, addresseeId);
            await AcceptIncomingAsync(addresseeId, requestId);
        }

        public async Task<List<FriendRequestResponseDto>?> GetPendingForUserAsync(long userId)
        {
            LoginAs(userId);
            return await _friendRequestService.GetPendingAsync();
        }

        public async Task<List<FriendRequestResponseDto>?> GetIgnoredIncomingForUserAsync(long userId)
        {
            LoginAs(userId);
            return await _friendRequestService.GetIgnoredIncomingAsync();
        }

        public async Task<FriendRequest?> GetStoredRequestForPairAsync(long userId, long otherUserId) =>
            await _friendRequestRepository.GetForUserPairAsync(userId, otherUserId);

        public async Task AssertRequestExistsAsync(long userId, long otherUserId, bool? isPending = null)
        {
            var stored = await GetStoredRequestForPairAsync(userId, otherUserId);
            Assert.NotNull(stored);
            if (isPending.HasValue)
                Assert.Equal(isPending.Value, stored.IsPending);
        }

        public async Task AssertNoRequestForPairAsync(long userId, long otherUserId)
        {
            Assert.Null(await GetStoredRequestForPairAsync(userId, otherUserId));
        }

        public async Task AssertFriendshipExistsAsync(long userId, long otherUserId, bool expected = true)
        {
            var friendship = await _friendshipRepository.GetForUserPairAsync(userId, otherUserId);
            if (expected)
                Assert.NotNull(friendship);
            else
                Assert.Null(friendship);
        }

        public async Task AssertPendingDtoForUserAsync(
            long viewerId,
            long otherUserId,
            bool appearsPending,
            bool? isIncoming = null)
        {
            var pending = await GetPendingForUserAsync(viewerId);
            Assert.NotNull(pending);
            var dto = pending.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
            Assert.Equal(appearsPending, dto.IsPending);
        }
    }
}

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
