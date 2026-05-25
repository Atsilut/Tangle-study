using Microsoft.EntityFrameworkCore;

namespace Api.Global.Db;

public static class AppDbContextTransactionExtensions
{
    public static Task ExecuteInTransactionAsync(this AppDbContext db, Func<Task> action)
    {
        if (!db.Database.IsRelational())
            return action();
        if (db.Database.CurrentTransaction is not null)
            return action();

        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
