using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Repositories;

public sealed class GroupBoardRepositoryUnitTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAndGetByGroupAndId_PersistsBoard()
    {
        // Arrange
        await using var db = CreateDb();
        var group = new Group("g", "d", GroupVisibility.Private);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var board = new GroupBoard(group.Id, "Announcements", BoardVisibility.MembersOnly, "Main board");
        db.GroupBoards.Add(board);
        await db.SaveChangesAsync();

        // Act
        var loaded = await db.GroupBoards.FirstOrDefaultAsync(b => b.GroupId == group.Id && b.Id == board.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Announcements", loaded!.Name);
        Assert.Equal(BoardVisibility.MembersOnly, loaded.Visibility);
        Assert.Equal("Main board", loaded.Description);
    }

    [Fact]
    public async Task ExistsByNameAsync_DetectsDuplicateWithinGroup()
    {
        // Arrange
        var repo = new FakeGroupBoardRepository();
        var board = new GroupBoard(1, "General", BoardVisibility.ForAll);
        await repo.CreateAsync(board);

        // Act
        var existsInGroup = await repo.ExistsByNameAsync(1, "General");
        var notOtherName = await repo.ExistsByNameAsync(1, "Other");
        var notOtherGroup = await repo.ExistsByNameAsync(2, "General");
        var excludedSelf = await repo.ExistsByNameAsync(1, "General", excludeBoardId: board.Id);

        // Assert
        Assert.True(existsInGroup);
        Assert.False(notOtherName);
        Assert.False(notOtherGroup);
        Assert.False(excludedSelf);
    }

    [Fact]
    public async Task DeleteAllByGroup_RemovesOnlyTargetGroupBoards()
    {
        // Arrange
        var repo = new FakeGroupBoardRepository();
        await repo.CreateAsync(new GroupBoard(1, "A1", BoardVisibility.MembersOnly));
        await repo.CreateAsync(new GroupBoard(1, "A2", BoardVisibility.AdminOnly));
        await repo.CreateAsync(new GroupBoard(2, "B1", BoardVisibility.ForAll));

        // Act
        await repo.DeleteAllByGroupAsync(1);

        // Assert
        var remaining = await repo.GetByGroupAsync(2);
        Assert.Single(remaining);
        Assert.Equal("B1", remaining[0].Name);
        Assert.Empty(await repo.GetByGroupAsync(1));
    }
}
