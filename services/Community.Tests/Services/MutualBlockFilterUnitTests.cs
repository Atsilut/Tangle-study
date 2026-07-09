using Community.Service;

namespace Community.Tests.Services;

public sealed class MutualBlockFilterUnitTests
{
    private sealed record Item(long AuthorId, string Name);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FilterByMutualBlockAsync_ReturnsAll_WhenViewerIsNull()
    {
        Item[] items = [new(1, "a"), new(2, "b")];

        var result = await MutualBlockFilter.FilterByMutualBlockAsync(
            viewerUserId: null,
            items,
            item => item.AuthorId,
            (_, _, _) => throw new InvalidOperationException("should not call social"),
            Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterByMutualBlockAsync_ReturnsAll_WhenItemsEmpty()
    {
        var result = await MutualBlockFilter.FilterByMutualBlockAsync(
            viewerUserId: 1,
            Array.Empty<Item>(),
            item => item.AuthorId,
            (_, _, _) => throw new InvalidOperationException("should not call social"),
            Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FilterByMutualBlockAsync_ReturnsAll_WhenNoBlocks()
    {
        Item[] items = [new(1, "a"), new(2, "b")];

        var result = await MutualBlockFilter.FilterByMutualBlockAsync(
            viewerUserId: 9,
            items,
            item => item.AuthorId,
            (_, _, _) => Task.FromResult<IReadOnlyCollection<long>>([]),
            Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterByMutualBlockAsync_RemovesBlockedAuthors()
    {
        Item[] items = [new(1, "a"), new(2, "b"), new(3, "c")];

        var result = await MutualBlockFilter.FilterByMutualBlockAsync(
            viewerUserId: 9,
            items,
            item => item.AuthorId,
            (_, _, _) => Task.FromResult<IReadOnlyCollection<long>>([2]),
            Ct);

        Assert.Equal([1L, 3L], result.Select(i => i.AuthorId));
    }

    [Fact]
    public async Task IsAuthorBlockedByViewerAsync_ReturnsFalse_WhenViewerIsNull()
    {
        var blocked = await MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            viewerUserId: null,
            authorUserId: 2,
            (_, _, _) => throw new InvalidOperationException("should not call social"),
            Ct);

        Assert.False(blocked);
    }

    [Fact]
    public async Task IsAuthorBlockedByViewerAsync_ReturnsFalse_WhenViewerIsAuthor()
    {
        var blocked = await MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            viewerUserId: 2,
            authorUserId: 2,
            (_, _, _) => throw new InvalidOperationException("should not call social"),
            Ct);

        Assert.False(blocked);
    }

    [Fact]
    public async Task IsAuthorBlockedByViewerAsync_ReturnsTrue_WhenAuthorBlocked()
    {
        var blocked = await MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            viewerUserId: 1,
            authorUserId: 2,
            (_, ids, _) =>
            {
                Assert.Equal([2L], ids);
                return Task.FromResult<IReadOnlyCollection<long>>([2]);
            },
            Ct);

        Assert.True(blocked);
    }

    [Fact]
    public async Task IsAuthorBlockedByViewerAsync_ReturnsFalse_WhenAuthorNotBlocked()
    {
        var blocked = await MutualBlockFilter.IsAuthorBlockedByViewerAsync(
            viewerUserId: 1,
            authorUserId: 2,
            (_, _, _) => Task.FromResult<IReadOnlyCollection<long>>([]),
            Ct);

        Assert.False(blocked);
    }
}
