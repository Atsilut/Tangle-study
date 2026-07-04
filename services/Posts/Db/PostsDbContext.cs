using Microsoft.EntityFrameworkCore;
using Posts.Entities;

namespace Posts.Db;

public class PostsDbContext(DbContextOptions<PostsDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("posts");

        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("Posts");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Content).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => p.GroupId);
            entity.HasIndex(p => p.GroupBoardId);
            entity.HasIndex(p => new { p.GroupId, p.GroupBoardId });
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comments");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Content).HasMaxLength(1000).IsRequired();
            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.PostId);
            entity.HasIndex(c => c.ParentId);
            entity.HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(c => c.Parent)
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
