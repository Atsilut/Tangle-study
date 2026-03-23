using Api.Domain.Users.Domain;

namespace Api.Domain.Users.Repository
{
    public class UserRepository
    {
        private readonly Global.Db.AppDbContext _context;

        public UserRepository(Global.Db.AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}
