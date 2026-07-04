using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Global.Db
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
    }
}
