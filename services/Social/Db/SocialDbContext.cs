using Microsoft.EntityFrameworkCore;
using Social.Entities;

namespace Social.Db;

public class SocialDbContext(DbContextOptions<SocialDbContext> options) : DbContext(options)
{
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("social");

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("Friendships", t => t.HasCheckConstraint(
                "CK_Friendships_UserLowLtUserHigh",
                "\"UserLowId\" < \"UserHighId\""));
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.UserLowId, f.UserHighId }).IsUnique();
            entity.HasIndex(f => f.UserLowId);
            entity.HasIndex(f => f.UserHighId);
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.ToTable("FriendRequests");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.RequesterId, r.AddresseeId }).IsUnique();
            entity.HasIndex(r => r.RequesterId);
            entity.HasIndex(r => r.AddresseeId);
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.ToTable("UserBlocks");
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.BlockerId, b.BlockedUserId }).IsUnique();
            entity.HasIndex(b => b.BlockerId);
            entity.HasIndex(b => b.BlockedUserId);
        });
    }
}
