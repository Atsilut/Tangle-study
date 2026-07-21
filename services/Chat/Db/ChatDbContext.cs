using Chat.Entities;
using Microsoft.EntityFrameworkCore;
using Tangle.AspNetCore.Outbox;

namespace Chat.Db;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomParticipant> ChatRoomParticipants => Set<ChatRoomParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageReceipt> ChatMessageReceipts => Set<ChatMessageReceipt>();
    public DbSet<ChatMessageEdit> ChatMessageEdits => Set<ChatMessageEdit>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat");

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms");
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ChatRooms_DirectUserLowLtUserHigh",
                "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL AND \"UserLowId\" < \"UserHighId\")"));
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ChatRooms_PlatformGroupIdByKind",
                "(\"Kind\" = 2 AND \"PlatformGroupId\" IS NOT NULL) OR (\"Kind\" <> 2 AND \"PlatformGroupId\" IS NULL)"));
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ChatRooms_DirectPairOnlyForDirect",
                "\"Kind\" <> 0 OR (\"UserLowId\" IS NOT NULL AND \"UserHighId\" IS NOT NULL)"));
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ChatRooms_NoDirectPairForNonDirect",
                "\"Kind\" = 0 OR (\"UserLowId\" IS NULL AND \"UserHighId\" IS NULL)"));
        });

        modelBuilder.Entity<ChatRoomParticipant>(entity =>
        {
            entity.ToTable("ChatRoomParticipants");
            entity.HasOne(p => p.ChatRoom)
                .WithMany(r => r.Participants)
                .HasForeignKey(p => p.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasOne(m => m.ChatRoom)
                .WithMany()
                .HasForeignKey(m => m.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.ChatRoomId, m.Id })
                .IsDescending(false, true);
        });

        modelBuilder.Entity<ChatMessageReceipt>(entity =>
        {
            entity.ToTable("ChatMessageReceipts");
            entity.HasOne(r => r.ChatMessage)
                .WithMany()
                .HasForeignKey(r => r.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessageEdit>(entity =>
        {
            entity.ToTable("ChatMessageEdits");
            entity.HasOne(e => e.ChatMessage)
                .WithMany()
                .HasForeignKey(e => e.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ConfigureOutbox();
    }
}
