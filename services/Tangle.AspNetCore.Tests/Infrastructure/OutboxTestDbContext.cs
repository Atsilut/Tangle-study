using Microsoft.EntityFrameworkCore;
using Tangle.AspNetCore.Outbox;

namespace Tangle.AspNetCore.Tests.Infrastructure;

public sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("outbox_test");
        modelBuilder.ConfigureOutbox();
    }
}
