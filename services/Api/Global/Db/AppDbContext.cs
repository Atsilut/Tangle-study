using Api.Domain.Users.Domain;
using Api.Domain.Posts.Domain;
using Microsoft.EntityFrameworkCore;
using Api.Domain.Comments.Domain;

namespace Api.Global.Db
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Rely on EF Core conventions + entity annotations.
    }
}
