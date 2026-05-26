using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupBoardServiceUnitTests
{
    // --- Create default visibility matrix ---

    public static TheoryData<GroupVisibility, BoardVisibility?, BoardVisibility> CreateDefaultVisibilityData =>
        new()
        {
            { GroupVisibility.Public, null, BoardVisibility.ForAll },
            { GroupVisibility.Private, null, BoardVisibility.MembersOnly },
            { GroupVisibility.Public, BoardVisibility.AdminOnly, BoardVisibility.AdminOnly },
            { GroupVisibility.Private, BoardVisibility.ForAll, BoardVisibility.ForAll },
        };

    [Theory]
    [MemberData(nameof(CreateDefaultVisibilityData))]
    public async Task CreateAsync_DefaultVisibility_Matrix(
        GroupVisibility groupVisibility,
        BoardVisibility? requestVisibility,
        BoardVisibility expectedVisibility)
    {
        var scenario = await GroupTestScenario.CreateAsync($"board_create_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: false, includeMember: false);
        scenario.LoginAs(GroupActorRole.Owner);

        var dto = await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "board",
            Description = "desc",
            Visibility = requestVisibility,
        });

        Assert.Equal(expectedVisibility, dto.Visibility);
        var persisted = await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, dto.Id);
        Assert.NotNull(persisted);
        Assert.Equal(expectedVisibility, persisted!.Visibility);
    }

    // --- List filtering ---

    [Fact]
    public async Task ListAsync_FiltersBoardsForMemberOnPrivateGroup()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_list_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        await scenario.SeedBoardAsync(group.Id, "AdminOnly", BoardVisibility.AdminOnly);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);
        await scenario.SeedBoardAsync(group.Id, "ForAll", BoardVisibility.ForAll);

        scenario.LoginAs(GroupActorRole.Member);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, b => b.Name == "MembersOnly");
        Assert.Contains(listed, b => b.Name == "ForAll");
        Assert.DoesNotContain(listed, b => b.Name == "AdminOnly");
    }

    [Fact]
    public async Task ListAsync_WhenOnlyAdminOnlyBoard_ReturnsEmptyForMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_list_admin_only");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeMember: true);
        await scenario.SeedBoardAsync(group.Id, "AdminOnly", BoardVisibility.AdminOnly);

        scenario.LoginAs(GroupActorRole.Member);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Empty(listed);
    }

    [Fact]
    public async Task ListAsync_AdminSeesAdminAndMembersBoards()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_list_admin");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        await scenario.SeedBoardAsync(group.Id, "AdminOnly", BoardVisibility.AdminOnly);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        scenario.LoginAs(GroupActorRole.Admin);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, b => b.Name == "AdminOnly");
        Assert.Contains(listed, b => b.Name == "MembersOnly");
    }

    [Fact]
    public async Task ListAsync_StrangerOnPublicGroupSeesForAllBoard()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_list_stranger");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: false, includeMember: false);
        await scenario.SeedBoardAsync(group.Id, "ForAll", BoardVisibility.ForAll);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        scenario.LoginAs(GroupActorRole.Stranger);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Single(listed);
        Assert.Equal("ForAll", listed[0].Name);
    }

    [Fact]
    public async Task ListAsync_AnonymousOnPublicGroupSeesForAllOnly()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_list_anon");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeMember: false);
        await scenario.SeedBoardAsync(group.Id, "ForAll", BoardVisibility.ForAll);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        scenario.LoginAnonymous();
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Single(listed);
        Assert.Equal("ForAll", listed[0].Name);
    }

    // --- CRUD authorization matrix ---

    public static TheoryData<GroupActorRole, BoardCrudOperation, GroupExpectedOutcome> CrudAuthorizationData =>
        new()
        {
            { GroupActorRole.Owner, BoardCrudOperation.Create, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, BoardCrudOperation.Create, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, BoardCrudOperation.Create, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, BoardCrudOperation.Create, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, BoardCrudOperation.Update, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, BoardCrudOperation.Update, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, BoardCrudOperation.Delete, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, BoardCrudOperation.Delete, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(CrudAuthorizationData))]
    public async Task BoardCrudAuthorization_Matrix(
        GroupActorRole actor,
        BoardCrudOperation operation,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"board_crud_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var board = await scenario.SeedBoardAsync(group.Id, "Original", BoardVisibility.MembersOnly);
        var countBefore = (await scenario.BoardRepository.GetByGroupAsync(group.Id)).Count;
        scenario.LoginAs(actor);

        if (operation == BoardCrudOperation.Create)
        {
            if (expected == GroupExpectedOutcome.Ok)
            {
                var created = await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
                {
                    Name = "created",
                    Description = "new",
                });
                Assert.Equal("created", created.Name);
                Assert.Equal(countBefore + 1, (await scenario.BoardRepository.GetByGroupAsync(group.Id)).Count);
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "denied" }));
                Assert.Equal(countBefore, (await scenario.BoardRepository.GetByGroupAsync(group.Id)).Count);
            }

            return;
        }

        if (operation == BoardCrudOperation.Update)
        {
            if (expected == GroupExpectedOutcome.Ok)
            {
                var updated = await scenario.BoardService.UpdateAsync(group.Id, board.Id, new GroupBoardPatchRequestDto
                {
                    Name = "Updated",
                    Description = "patched",
                    Visibility = BoardVisibility.AdminOnly,
                });
                Assert.Equal("Updated", updated.Name);
                Assert.Equal(BoardVisibility.AdminOnly, updated.Visibility);
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.BoardService.UpdateAsync(group.Id, board.Id, new GroupBoardPatchRequestDto
                    {
                        Name = "Hacked",
                        Visibility = BoardVisibility.ForAll,
                    }));
                var unchanged = await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, board.Id);
                Assert.Equal("Original", unchanged!.Name);
                Assert.Equal(BoardVisibility.MembersOnly, unchanged.Visibility);
            }

            return;
        }

        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.BoardService.DeleteAsync(group.Id, board.Id);
            Assert.Null(await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, board.Id));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.BoardService.DeleteAsync(group.Id, board.Id));
            Assert.NotNull(await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, board.Id));
        }
    }

    // --- CRUD facts ---

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_dup");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "Dup" });

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "Dup" }));
    }

    [Fact]
    public async Task UpdateAsync_ConflictingName_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_conflict");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        scenario.LoginAs(GroupActorRole.Owner);
        var first = await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "First" });
        var second = await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "Second" });

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.BoardService.UpdateAsync(group.Id, second.Id, new GroupBoardPatchRequestDto
            {
                Name = "First",
                Visibility = BoardVisibility.MembersOnly,
            }));
        Assert.Equal("Second", (await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, second.Id))!.Name);
        Assert.NotNull(await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, first.Id));
    }

    [Fact]
    public async Task ListAsync_WhenGroupMissing_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("board_missing");
        scenario.LoginAs(GroupActorRole.Member);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardService.ListAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }
}
