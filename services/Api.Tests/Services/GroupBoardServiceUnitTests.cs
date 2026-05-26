using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;
namespace Api.Tests.Services;

public sealed class GroupBoardServiceUnitTests
{
    public static TheoryData<string, GroupVisibility, BoardVisibility?, BoardVisibility> CreateDefaultVisibilityData =>
        new()
        {
            { "BS-C-01", GroupVisibility.Public, null, BoardVisibility.ForAll },
            { "BS-C-02", GroupVisibility.Private, null, BoardVisibility.MembersOnly },
            { "BS-C-03", GroupVisibility.Public, BoardVisibility.AdminOnly, BoardVisibility.AdminOnly },
            { "BS-C-04", GroupVisibility.Private, BoardVisibility.ForAll, BoardVisibility.ForAll },
        };

    [Theory]
    [MemberData(nameof(CreateDefaultVisibilityData))]
    public async Task Create_DefaultVisibility_Matrix(
        string caseId,
        GroupVisibility groupVisibility,
        BoardVisibility? requestVisibility,
        BoardVisibility expectedVisibility)
    {
        var scenario = await GroupTestScenario.CreateAsync($"bsc_{caseId}");
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: false, includeMember: false);
        scenario.LoginAs(GroupActorRole.Owner);

        var dto = await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = $"board_{caseId}",
            Description = "desc",
            Visibility = requestVisibility,
        });

        Assert.Equal(expectedVisibility, dto.Visibility);
        Assert.Equal($"board_{caseId}", dto.Name);

        var persisted = await scenario.BoardRepository.GetByGroupAndIdAsync(group.Id, dto.Id);
        Assert.NotNull(persisted);
        Assert.Equal(expectedVisibility, persisted!.Visibility);
    }

    [Fact]
    public async Task BS_L01_List_FiltersBoardsForMemberOnPrivateGroup()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsl01");
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
    public async Task BS_L02_List_MemberSeesNoAdminOnlyBoards()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsl02");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeMember: true);
        await scenario.SeedBoardAsync(group.Id, "AdminOnly", BoardVisibility.AdminOnly);

        scenario.LoginAs(GroupActorRole.Member);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Empty(listed);
    }

    [Fact]
    public async Task BS_L03_List_AdminSeesAdminAndMembersBoards()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsl03");
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
    public async Task BS_L04_List_StrangerOnPublicGroupSeesForAllBoard()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsl04");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: false, includeMember: false);
        await scenario.SeedBoardAsync(group.Id, "ForAll", BoardVisibility.ForAll);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        scenario.LoginAs(GroupActorRole.Stranger);
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Single(listed);
        Assert.Equal("ForAll", listed[0].Name);
    }

    [Fact]
    public async Task BS_L05_List_AnonymousOnPublicGroupSeesForAllOnly()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsl05");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeMember: false);
        await scenario.SeedBoardAsync(group.Id, "ForAll", BoardVisibility.ForAll);
        await scenario.SeedBoardAsync(group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        scenario.LoginAnonymous();
        var listed = await scenario.BoardService.ListAsync(group.Id);

        Assert.Single(listed);
        Assert.Equal("ForAll", listed[0].Name);
    }

    public static TheoryData<string, GroupActorRole, BoardCrudOperation, GroupExpectedOutcome> CrudAuthorizationData =>
        new()
        {
            { "BS-M-01", GroupActorRole.Owner, BoardCrudOperation.Create, GroupExpectedOutcome.Ok },
            { "BS-M-02", GroupActorRole.Admin, BoardCrudOperation.Create, GroupExpectedOutcome.Ok },
            { "BS-M-03", GroupActorRole.Member, BoardCrudOperation.Create, GroupExpectedOutcome.Unauthorized },
            { "BS-M-04", GroupActorRole.Stranger, BoardCrudOperation.Create, GroupExpectedOutcome.Unauthorized },
            { "BS-M-05", GroupActorRole.Owner, BoardCrudOperation.Update, GroupExpectedOutcome.Ok },
            { "BS-M-06", GroupActorRole.Member, BoardCrudOperation.Update, GroupExpectedOutcome.Unauthorized },
            { "BS-M-07", GroupActorRole.Owner, BoardCrudOperation.Delete, GroupExpectedOutcome.Ok },
            { "BS-M-08", GroupActorRole.Member, BoardCrudOperation.Delete, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(CrudAuthorizationData))]
    public async Task CrudAuthorization_Matrix(
        string caseId,
        GroupActorRole actor,
        BoardCrudOperation operation,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"bsm_{caseId}");
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
                    Name = $"created_{caseId}",
                    Description = "new",
                });
                Assert.Equal($"created_{caseId}", created.Name);
                Assert.Equal(countBefore + 1, (await scenario.BoardRepository.GetByGroupAsync(group.Id)).Count);
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
                    {
                        Name = $"denied_{caseId}",
                    }));
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

    [Fact]
    public async Task BS_M09_Create_DuplicateName_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsm09");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "Dup" });

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.BoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto { Name = "Dup" }));
    }

    [Fact]
    public async Task BS_M10_Update_ConflictingName_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsm10");
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
    public async Task BS_M11_List_MissingGroup_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsm11");
        scenario.LoginAs(GroupActorRole.Member);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardService.ListAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task BS_M12_List_StrangerOnMissingGroup_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("bsm12");
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.BoardService.ListAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }
}

public enum BoardCrudOperation
{
    Create,
    Update,
    Delete,
}
