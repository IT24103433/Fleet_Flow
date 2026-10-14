using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace IdentityService.Api.Data;
public static class AccountWriteTransaction
{
    public static async Task<IDbContextTransaction?> BeginAsync(IdentityDbContext db)
    {
        if (!db.Database.IsRelational()) return null;
        var tx = await db.Database.BeginTransactionAsync();
        try {
            if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-admin-account-change'))");
            return tx;
        } catch { await tx.DisposeAsync(); throw; }
    }
}
