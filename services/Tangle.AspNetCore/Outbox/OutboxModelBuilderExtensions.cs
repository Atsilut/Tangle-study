using Microsoft.EntityFrameworkCore;

namespace Tangle.AspNetCore.Outbox;

public static class OutboxModelBuilderExtensions
{
    public static void ConfigureOutbox(this ModelBuilder modelBuilder, string? tableName = "OutboxMessages")
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Target).HasMaxLength(256).IsRequired();
            entity.Property(m => m.PayloadJson).IsRequired();
            entity.Property(m => m.LastError).HasMaxLength(2000);
            entity.HasIndex(m => new { m.ProcessedAt, m.DeadLetteredAt, m.Id });
        });
    }
}
