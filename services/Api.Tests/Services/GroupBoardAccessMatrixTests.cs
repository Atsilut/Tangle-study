using Api.Domain.Groups.Domain;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupBoardAccessMatrixTests
{
    // --- View access matrix ---

    public static TheoryData<GroupVisibility, BoardVisibility, GroupActorRole, bool> ViewAccessMatrixData =>
        new()
        {
            { GroupVisibility.Public, BoardVisibility.ForAll, GroupActorRole.Anonymous, true },
            { GroupVisibility.Public, BoardVisibility.ForAll, GroupActorRole.Stranger, true },
            { GroupVisibility.Public, BoardVisibility.ForAll, GroupActorRole.Member, true },
            { GroupVisibility.Public, BoardVisibility.ForAll, GroupActorRole.Admin, true },
            { GroupVisibility.Public, BoardVisibility.ForAll, GroupActorRole.Owner, true },
            { GroupVisibility.Private, BoardVisibility.ForAll, GroupActorRole.Anonymous, false },
            { GroupVisibility.Private, BoardVisibility.ForAll, GroupActorRole.Stranger, false },
            { GroupVisibility.Private, BoardVisibility.ForAll, GroupActorRole.Member, true },
            { GroupVisibility.Private, BoardVisibility.ForAll, GroupActorRole.Admin, true },
            { GroupVisibility.Private, BoardVisibility.ForAll, GroupActorRole.Owner, true },
            { GroupVisibility.Public, BoardVisibility.MembersOnly, GroupActorRole.Anonymous, false },
            { GroupVisibility.Public, BoardVisibility.MembersOnly, GroupActorRole.Stranger, false },
            { GroupVisibility.Public, BoardVisibility.MembersOnly, GroupActorRole.Member, true },
            { GroupVisibility.Public, BoardVisibility.MembersOnly, GroupActorRole.Admin, true },
            { GroupVisibility.Public, BoardVisibility.MembersOnly, GroupActorRole.Owner, true },
            { GroupVisibility.Private, BoardVisibility.MembersOnly, GroupActorRole.Anonymous, false },
            { GroupVisibility.Private, BoardVisibility.MembersOnly, GroupActorRole.Stranger, false },
            { GroupVisibility.Private, BoardVisibility.MembersOnly, GroupActorRole.Member, true },
            { GroupVisibility.Private, BoardVisibility.MembersOnly, GroupActorRole.Admin, true },
            { GroupVisibility.Private, BoardVisibility.MembersOnly, GroupActorRole.Owner, true },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, GroupActorRole.Anonymous, false },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, GroupActorRole.Stranger, false },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, GroupActorRole.Member, false },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, GroupActorRole.Admin, true },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, GroupActorRole.Owner, true },
            { GroupVisibility.Private, BoardVisibility.AdminOnly, GroupActorRole.Anonymous, false },
            { GroupVisibility.Private, BoardVisibility.AdminOnly, GroupActorRole.Stranger, false },
            { GroupVisibility.Private, BoardVisibility.AdminOnly, GroupActorRole.Member, false },
            { GroupVisibility.Private, BoardVisibility.AdminOnly, GroupActorRole.Admin, true },
            { GroupVisibility.Private, BoardVisibility.AdminOnly, GroupActorRole.Owner, true },
        };

    [Theory]
    [MemberData(nameof(ViewAccessMatrixData))]
    public async Task TryCanViewBoard_MatchesExpectedForVisibilityAndRole(
        GroupVisibility groupVisibility,
        BoardVisibility boardVisibility,
        GroupActorRole actor,
        bool canView)
    {
        var scenario = await GroupTestScenario.CreateAsync($"view_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: true, includeMember: true);
        var board = await scenario.SeedBoardAsync(group.Id, "board", boardVisibility);
        scenario.LoginAs(actor);

        var tryResult = await scenario.BoardAccess.TryCanViewBoardAsync(group.Id, board.Id);
        Assert.Equal(canView, tryResult);

        if (canView)
        {
            await scenario.BoardAccess.EnsureCanViewBoardAsync(group.Id, board.Id);
            await scenario.BoardAccess.EnsureCanWritePostAsync(group.Id, board.Id);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.BoardAccess.EnsureCanViewBoardAsync(group.Id, board.Id));
            Assert.Equal("Unauthorized access", ex.Message);
        }
    }

    // --- Infrastructure ---

    [Fact]
    public async Task TryCanViewBoard_WhenGroupMissing_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_grp");
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.TryCanViewBoardAsync(99999, 1));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task TryCanViewBoard_WhenBoardMissing_ReturnsFalse()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_board");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        scenario.LoginAs(GroupActorRole.Owner);

        Assert.False(await scenario.BoardAccess.TryCanViewBoardAsync(group.Id, 99999));
    }

    [Fact]
    public async Task EnsureCanViewBoard_WhenBoardMissing_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_ensure");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.EnsureCanViewBoardAsync(group.Id, 99999));
        Assert.Equal("Board not found", ex.Message);
    }

    [Fact]
    public async Task EnsureCanWritePost_WhenViewAllowed_Succeeds()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_write_ok");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        var board = await scenario.SeedBoardAsync(group.Id, "public_for_all", BoardVisibility.ForAll);
        scenario.LoginAnonymous();

        await scenario.BoardAccess.EnsureCanWritePostAsync(group.Id, board.Id);
    }

    [Fact]
    public async Task EnsureCanWritePost_WhenViewDenied_ThrowsUnauthorized()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_write_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeMember: true);
        var board = await scenario.SeedBoardAsync(group.Id, "admin_only", BoardVisibility.AdminOnly);
        scenario.LoginAs(GroupActorRole.Member);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.BoardAccess.EnsureCanWritePostAsync(group.Id, board.Id));
    }

    [Fact]
    public async Task GetBoardOrThrow_WhenBoardMissing_ThrowsBoardNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_get_board");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.GetBoardOrThrowAsync(group.Id, 99999));
        Assert.Equal("Board not found", ex.Message);
    }

    [Fact]
    public async Task GetBoardOrThrow_WhenGroupMissing_ThrowsGroupNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("view_get_group");

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.GetBoardOrThrowAsync(99999, 1));
        Assert.Equal("Group not found", ex.Message);
    }
}
