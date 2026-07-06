using Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Media.Db;

public class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("media");

        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.ToTable("MediaAssets");
            entity.HasIndex(m => m.UploaderId);
            entity.HasIndex(m => m.PostId);
            entity.HasIndex(m => m.CommentId);
            entity.HasIndex(m => m.ChatMessageId);
        });
    }
}
