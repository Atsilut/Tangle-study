using Microsoft.EntityFrameworkCore;
using Group.Entities;

namespace Group.Db;

public class GroupDbContext(DbContextOptions<GroupDbContext> options) : DbContext(options)
{
    public DbSet<Entities.Group> Groups => Set<Entities.Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
    public DbSet<GroupApplication> GroupApplications => Set<GroupApplication>();
    public DbSet<GroupBlacklist> GroupBlacklists => Set<GroupBlacklist>();
    public DbSet<GroupBoard> GroupBoards => Set<GroupBoard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("group");

        modelBuilder.Entity<Entities.Group>(entity =>
        {
            entity.ToTable("Groups");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).IsRequired();
            entity.Property(g => g.Description).IsRequired();
            entity.HasMany(g => g.Members)
                .WithOne(m => m.Group)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.ToTable("GroupMembers");
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
            entity.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<GroupInvitation>(entity =>
        {
            entity.ToTable("GroupInvitations");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.GroupId, i.InviteeId }).IsUnique();
            entity.HasIndex(i => i.InviteeId);
            entity.HasIndex(i => i.InviterId);
            entity.HasOne(i => i.Group)
                .WithMany()
                .HasForeignKey(i => i.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GroupApplication>(entity =>
        {
            entity.ToTable("GroupApplications");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.GroupId, a.ApplicantId }).IsUnique();
            entity.HasIndex(a => a.ApplicantId);
            entity.HasOne(a => a.Group)
                .WithMany()
                .HasForeignKey(a => a.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GroupBlacklist>(entity =>
        {
            entity.ToTable("GroupBlacklists");
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.GroupId, b.UserId }).IsUnique();
            entity.HasIndex(b => b.UserId);
            entity.HasOne(b => b.Group)
                .WithMany()
                .HasForeignKey(b => b.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GroupBoard>(entity =>
        {
            entity.ToTable("GroupBoards");
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.GroupId, b.Name }).IsUnique();
            entity.Property(b => b.Name).IsRequired();
            entity.HasOne(b => b.Group)
                .WithMany()
                .HasForeignKey(b => b.GroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
