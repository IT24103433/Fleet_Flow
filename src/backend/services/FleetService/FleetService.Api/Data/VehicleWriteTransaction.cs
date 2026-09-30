using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FleetService.Api.Data;

// Every write that affects rental eligibility takes this lock before reading mutable state.
// The vehicle row is the common lock boundary across API instances and related records.
internal static class VehicleWriteTransaction
{
    public static async Task<IDbContextTransaction?> BeginAsync(
        FleetDbContext db, Guid vehicleId, CancellationToken ct = default)
    {
        // Existing unit tests use EF InMemory; concurrency guarantees are tested on PostgreSQL.
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return null;
        if (!db.Database.IsNpgsql())
            throw new InvalidOperationException("Vehicle write locking requires PostgreSQL.");

        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // No joins: PostgreSQL must lock only the vehicle, before related entities are loaded.
            await db.Vehicles.FromSqlInterpolated($"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
