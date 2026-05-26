using Api.Domain.Groups.Domain;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupBoardAccessMatrixTests
{
    public static TheoryData<string, GroupVisibility, BoardVisibility, BoardAccessActor, bool> ViewAccessMatrixData =>
        new()
        {
            { "BA-001", GroupVisibility.Public, BoardVisibility.ForAll, BoardAccessActor.Anonymous, true },
            { "BA-002", GroupVisibility.Public, BoardVisibility.ForAll, BoardAccessActor.Stranger, true },
            { "BA-003", GroupVisibility.Public, BoardVisibility.ForAll, BoardAccessActor.Member, true },
            { "BA-004", GroupVisibility.Public, BoardVisibility.ForAll, BoardAccessActor.Admin, true },
            { "BA-005", GroupVisibility.Public, BoardVisibility.ForAll, BoardAccessActor.Owner, true },
            { "BA-006", GroupVisibility.Private, BoardVisibility.ForAll, BoardAccessActor.Anonymous, false },
            { "BA-007", GroupVisibility.Private, BoardVisibility.ForAll, BoardAccessActor.Stranger, false },
            { "BA-008", GroupVisibility.Private, BoardVisibility.ForAll, BoardAccessActor.Member, true },
            { "BA-009", GroupVisibility.Private, BoardVisibility.ForAll, BoardAccessActor.Admin, true },
            { "BA-010", GroupVisibility.Private, BoardVisibility.ForAll, BoardAccessActor.Owner, true },
            { "BA-011", GroupVisibility.Public, BoardVisibility.MembersOnly, BoardAccessActor.Anonymous, false },
            { "BA-012", GroupVisibility.Public, BoardVisibility.MembersOnly, BoardAccessActor.Stranger, false },
            { "BA-013", GroupVisibility.Public, BoardVisibility.MembersOnly, BoardAccessActor.Member, true },
            { "BA-014", GroupVisibility.Public, BoardVisibility.MembersOnly, BoardAccessActor.Admin, true },
            { "BA-015", GroupVisibility.Public, BoardVisibility.MembersOnly, BoardAccessActor.Owner, true },
            { "BA-016", GroupVisibility.Private, BoardVisibility.MembersOnly, BoardAccessActor.Anonymous, false },
            { "BA-017", GroupVisibility.Private, BoardVisibility.MembersOnly, BoardAccessActor.Stranger, false },
            { "BA-018", GroupVisibility.Private, BoardVisibility.MembersOnly, BoardAccessActor.Member, true },
            { "BA-019", GroupVisibility.Private, BoardVisibility.MembersOnly, BoardAccessActor.Admin, true },
            { "BA-020", GroupVisibility.Private, BoardVisibility.MembersOnly, BoardAccessActor.Owner, true },
            { "BA-021", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardAccessActor.Anonymous, false },
            { "BA-022", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardAccessActor.Stranger, false },
            { "BA-023", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardAccessActor.Member, false },
            { "BA-024", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardAccessActor.Admin, true },
            { "BA-025", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardAccessActor.Owner, true },
            { "BA-026", GroupVisibility.Private, BoardVisibility.AdminOnly, BoardAccessActor.Anonymous, false },
            { "BA-027", GroupVisibility.Private, BoardVisibility.AdminOnly, BoardAccessActor.Stranger, false },
            { "BA-028", GroupVisibility.Private, BoardVisibility.AdminOnly, BoardAccessActor.Member, false },
            { "BA-029", GroupVisibility.Private, BoardVisibility.AdminOnly, BoardAccessActor.Admin, true },
            { "BA-030", GroupVisibility.Private, BoardVisibility.AdminOnly, BoardAccessActor.Owner, true },
        };

    [Theory]
    [MemberData(nameof(ViewAccessMatrixData))]
    public async Task ViewAccess_Matrix(
        string caseId,
        GroupVisibility groupVisibility,
        BoardVisibility boardVisibility,
        BoardAccessActor actor,
        bool canView)
    {
        var scenario = await GroupTestScenario.CreateAsync($"ba_{caseId}");
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: true, includeMember: true);
        var board = await scenario.SeedBoardAsync(group.Id, $"board_{caseId}", boardVisibility);
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

    [Fact]
    public async Task BA031_UnknownGroup_ThrowsGroupNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba031");
        scenario.LoginAs(BoardAccessActor.Owner);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.TryCanViewBoardAsync(99999, 1));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task BA032_UnknownBoard_TryReturnsFalse_EnsureThrowsBoardNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba032");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        scenario.LoginAs(BoardAccessActor.Owner);

        Assert.False(await scenario.BoardAccess.TryCanViewBoardAsync(group.Id, 99999));

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.EnsureCanViewBoardAsync(group.Id, 99999));
        Assert.Equal("Board not found", ex.Message);
    }

    [Fact]
    public async Task BA033_EnsureCanWritePost_MatchesViewOnAllowedRow()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba033");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        var board = await scenario.SeedBoardAsync(group.Id, "public_for_all", BoardVisibility.ForAll);
        scenario.LoginAnonymous();

        await scenario.BoardAccess.EnsureCanWritePostAsync(group.Id, board.Id);
    }

    [Fact]
    public async Task BA034_EnsureCanWritePost_MatchesViewOnDeniedRow()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba034");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeMember: true);
        var board = await scenario.SeedBoardAsync(group.Id, "admin_only", BoardVisibility.AdminOnly);
        scenario.LoginAs(BoardAccessActor.Member);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.BoardAccess.EnsureCanWritePostAsync(group.Id, board.Id));
    }

    [Fact]
    public async Task BA035_GetBoardOrThrow_MissingBoard_ThrowsBoardNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba035");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.GetBoardOrThrowAsync(group.Id, 99999));
        Assert.Equal("Board not found", ex.Message);
    }

    [Fact]
    public async Task BA036_GetBoardOrThrow_MissingGroup_ThrowsGroupNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("ba036");

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardAccess.GetBoardOrThrowAsync(99999, 1));
        Assert.Equal("Group not found", ex.Message);
    }
}
