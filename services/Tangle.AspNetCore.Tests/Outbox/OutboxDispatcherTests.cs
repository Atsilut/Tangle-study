using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Outbox;
using Tangle.AspNetCore.Queue;
using Tangle.AspNetCore.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Tangle.AspNetCore.Tests.Outbox;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task DispatchBatch_ProcessesWorkQueueRow_SetsProcessedAt()
    {
        await using var db = CreateInMemoryDb();
        var workQueue = new FakeWorkQueue();
        var dispatcher = CreateDispatcher(db, workQueue, new FakeOutboxEventPublisher(), maxAttempts: 10);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Destination = OutboxDestination.WorkQueue,
            Target = "media.uploaded",
            PayloadJson = """{"mediaAssetId":1}""",
            EntityId = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dispatcher.DispatchBatchAsync(batchSize: 50, maxAttempts: 10, TestContext.Current.CancellationToken);

        var row = Assert.Single(db.OutboxMessages);
        Assert.NotNull(row.ProcessedAt);
        Assert.Null(row.DeadLetteredAt);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.LastError);
        var job = Assert.Single(workQueue.Enqueued);
        Assert.Equal("media.uploaded", job.StreamKey);
        Assert.Equal("""{"mediaAssetId":1}""", job.PayloadJson);
    }

    [Fact]
    public async Task DispatchBatch_ProcessesEventRow_SetsProcessedAt()
    {
        await using var db = CreateInMemoryDb();
        var events = new FakeOutboxEventPublisher();
        var dispatcher = CreateDispatcher(db, new FakeWorkQueue(), events, maxAttempts: 10);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Destination = OutboxDestination.Event,
            Target = "tangle:events:chat.message.created",
            PayloadJson = """{"messageId":42}""",
            EntityId = 42,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dispatcher.DispatchBatchAsync(batchSize: 50, maxAttempts: 10, TestContext.Current.CancellationToken);

        var row = Assert.Single(db.OutboxMessages);
        Assert.NotNull(row.ProcessedAt);
        var published = Assert.Single(events.Published);
        Assert.Equal("tangle:events:chat.message.created", published.Channel);
        Assert.Equal("""{"messageId":42}""", published.PayloadJson);
    }

    [Fact]
    public async Task DispatchBatch_IncrementsAttempts_WhenPublishFailsBelowMax()
    {
        await using var db = CreateInMemoryDb();
        var workQueue = new FakeWorkQueue();
        workQueue.FailNext(new InvalidOperationException("transient redis failure"), times: 1);
        var dispatcher = CreateDispatcher(db, workQueue, new FakeOutboxEventPublisher(), maxAttempts: 10);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Destination = OutboxDestination.WorkQueue,
            Target = "chat.message.created",
            PayloadJson = """{"messageId":7}""",
            EntityId = 7,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dispatcher.DispatchBatchAsync(batchSize: 50, maxAttempts: 10, TestContext.Current.CancellationToken);

        var row = Assert.Single(db.OutboxMessages);
        Assert.Null(row.ProcessedAt);
        Assert.Null(row.DeadLetteredAt);
        Assert.Equal(1, row.Attempts);
        Assert.Contains("transient redis failure", row.LastError);
        Assert.Empty(workQueue.Enqueued);
    }

    [Fact]
    public async Task DispatchBatch_DeadLetters_WhenMaxAttemptsReached()
    {
        await using var db = CreateInMemoryDb();
        var workQueue = new FakeWorkQueue();
        workQueue.FailNext(new InvalidOperationException("permanent failure"), times: 1);
        var dispatcher = CreateDispatcher(db, workQueue, new FakeOutboxEventPublisher(), maxAttempts: 1);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Destination = OutboxDestination.WorkQueue,
            Target = "media.uploaded",
            PayloadJson = """{"mediaAssetId":9}""",
            EntityId = 9,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dispatcher.DispatchBatchAsync(batchSize: 50, maxAttempts: 1, TestContext.Current.CancellationToken);

        var row = Assert.Single(db.OutboxMessages);
        Assert.Null(row.ProcessedAt);
        Assert.NotNull(row.DeadLetteredAt);
        Assert.Equal(1, row.Attempts);
        Assert.Contains("permanent failure", row.LastError);
    }

    [Fact]
    public async Task DispatchBatch_OrdersById_AndRespectsBatchSize()
    {
        await using var db = CreateInMemoryDb();
        var workQueue = new FakeWorkQueue();
        var dispatcher = CreateDispatcher(db, workQueue, new FakeOutboxEventPublisher(), maxAttempts: 10);

        for (var i = 1; i <= 5; i++)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Destination = OutboxDestination.WorkQueue,
                Target = "media.uploaded",
                PayloadJson = $$"""{"mediaAssetId":{{i}}}""",
                EntityId = i,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dispatcher.DispatchBatchAsync(batchSize: 3, maxAttempts: 10, TestContext.Current.CancellationToken);

        Assert.Equal(3, workQueue.Enqueued.Count);
        Assert.Equal("""{"mediaAssetId":1}""", workQueue.Enqueued[0].PayloadJson);
        Assert.Equal("""{"mediaAssetId":2}""", workQueue.Enqueued[1].PayloadJson);
        Assert.Equal("""{"mediaAssetId":3}""", workQueue.Enqueued[2].PayloadJson);

        var pending = db.OutboxMessages.Where(m => m.ProcessedAt == null).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, pending.Count);
        Assert.Equal(4, pending[0].EntityId);
        Assert.Equal(5, pending[1].EntityId);
    }

    [Fact]
    public async Task PruneProcessed_DeletesOldProcessed_KeepsRecentAndPending()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase("tangle_outbox_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var db = new OutboxTestDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        db.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Destination = OutboxDestination.WorkQueue,
                Target = "old",
                PayloadJson = "{}",
                CreatedAt = now.AddDays(-4),
                ProcessedAt = now.AddDays(-3),
            },
            new OutboxMessage
            {
                Destination = OutboxDestination.WorkQueue,
                Target = "recent",
                PayloadJson = "{}",
                CreatedAt = now.AddHours(-1),
                ProcessedAt = now.AddMinutes(-30),
            },
            new OutboxMessage
            {
                Destination = OutboxDestination.WorkQueue,
                Target = "pending",
                PayloadJson = "{}",
                CreatedAt = now,
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = CreateDispatcher(db, new FakeWorkQueue(), new FakeOutboxEventPublisher(), maxAttempts: 10);
        await dispatcher.PruneProcessedAsync(TimeSpan.FromHours(72), TestContext.Current.CancellationToken);

        var remaining = await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Target).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, m => m.Target == "recent");
        Assert.Contains(remaining, m => m.Target == "pending");
        Assert.DoesNotContain(remaining, m => m.Target == "old");
    }

    private static OutboxTestDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OutboxDispatcherHostedService<OutboxTestDbContext> CreateDispatcher(
        OutboxTestDbContext db,
        FakeWorkQueue workQueue,
        FakeOutboxEventPublisher events,
        int maxAttempts)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IWorkQueue>(workQueue);
        services.AddSingleton<IOutboxEventPublisher>(events);
        services.AddSingleton(new NoOpOutboxEventPublisher());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new OutboxDispatcherHostedService<OutboxTestDbContext>(
            scopeFactory,
            Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }),
            NullLogger<OutboxDispatcherHostedService<OutboxTestDbContext>>.Instance);
    }
}
