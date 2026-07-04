using Microsoft.EntityFrameworkCore;

namespace Social.Db;

public static class SocialDbContextTransactionExtensions
{
    public static Task ExecuteInTransactionAsync(this SocialDbContext db, Func<Task> action)
    {
        if (!db.Database.IsRelational()) return action();
        if (db.Database.CurrentTransaction is not null) return action();

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

    public static Task<T> ExecuteInTransactionAsync<T>(this SocialDbContext db, Func<Task<T>> action)
    {
        if (!db.Database.IsRelational()) return action();
        if (db.Database.CurrentTransaction is not null) return action();

        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
