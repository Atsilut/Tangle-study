using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Posts.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupBoardAccessIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
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
    public async Task BoardPostAccess_MatchesExpectedForVisibilityAndRole(
        GroupVisibility groupVisibility,
        BoardVisibility boardVisibility,
        GroupActorRole actor,
        bool canAccess)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"view_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: true, includeMember: true);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "board", boardVisibility);
        await scenario.LoginAsAsync(actor);

        // Act
        var listRes = await Client.GetAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts");
        var writeRes = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts",
            new GroupBoardPostCreateRequestDto { Title = "t", Content = "c" });

        // Assert
        // Board post endpoints require authentication; anonymous callers always get 401.
        if (actor == GroupActorRole.Anonymous)
        {
            await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.Unauthorized);
            await IntegrationAssertions.AssertStatusAsync(writeRes, HttpStatusCode.Unauthorized);
            return;
        }

        if (canAccess)
        {
            Assert.True(
                listRes.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
                $"list: {listRes.StatusCode}");
            await IntegrationAssertions.AssertStatusAsync(writeRes, HttpStatusCode.Created);
        }
        else
        {
            await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.Unauthorized);
            await IntegrationAssertions.AssertStatusAsync(writeRes, HttpStatusCode.Unauthorized);
        }
    }

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
    public async Task CreateBoard_DefaultVisibility_Matrix(
        GroupVisibility groupVisibility,
        BoardVisibility? requestVisibility,
        BoardVisibility expectedVisibility)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"board_create_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(groupVisibility, includeAdmin: false, includeMember: false);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var board = await scenario.CreateBoardAsync(group.Id, "board", requestVisibility);

        // Assert
        Assert.Equal(expectedVisibility, board.Visibility);
    }

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
        // Arrange
        var scenario = await CreateScenarioAsync($"board_crud_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(
            Factory, group.Id, "Original", BoardVisibility.MembersOnly);
        await scenario.LoginAsAsync(actor);

        var boardsBase = $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards";

        // Act
        HttpResponseMessage res = operation switch
        {
            BoardCrudOperation.Create => await Client.PostAsJsonAsync(boardsBase, new GroupBoardCreateRequestDto { Name = "created" }),
            BoardCrudOperation.Update => await Client.PatchAsJsonAsync($"{boardsBase}/{board.Id}", new GroupBoardPatchRequestDto
            {
                Name = "Updated",
                Visibility = BoardVisibility.AdminOnly,
            }),
            BoardCrudOperation.Delete => await Client.DeleteAsync($"{boardsBase}/{board.Id}"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        // Assert

        if (expected == GroupExpectedOutcome.Ok)
        {
            Assert.True(
                res.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.NoContent,
                res.StatusCode.ToString());
        }
        else
            await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    [Fact]
    public async Task ListBoards_FiltersForMemberOnPrivateGroup()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("board_list_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "AdminOnly", BoardVisibility.AdminOnly);
        await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);
        await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "ForAll", BoardVisibility.ForAll);
        await scenario.LoginAsAsync(GroupActorRole.Member);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var listed = await res.Content.ReadFromJsonAsync<List<GroupBoardResponseDto>>();
        Assert.Equal(2, listed!.Count);
        Assert.Contains(listed, b => b.Name == "MembersOnly");
        Assert.Contains(listed, b => b.Name == "ForAll");
    }

    [Fact]
    public async Task ListBoards_StrangerOnPublicSeesForAllOnly()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("board_list_stranger");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: false, includeMember: false);
        await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "ForAll", BoardVisibility.ForAll);
        await GroupIntegrationTestHelpers.SeedBoardAsync(Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var listed = await res.Content.ReadFromJsonAsync<List<GroupBoardResponseDto>>();
        Assert.Single(listed!);
        Assert.Equal("ForAll", listed[0].Name);
    }

    [Fact]
    public async Task CreateBoard_Returns409_WhenDuplicateName()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("board_dup");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        await scenario.LoginAsAsync(GroupActorRole.Owner);
        await scenario.CreateBoardAsync(group.Id, "Dup");

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "Dup" });

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }
}
