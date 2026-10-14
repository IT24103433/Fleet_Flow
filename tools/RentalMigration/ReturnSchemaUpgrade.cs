using Microsoft.EntityFrameworkCore;
namespace RentalMigration;
public static class ReturnSchemaUpgrade
{
    public static async Task ApplyAsync(string source, string target, CancellationToken ct = default)
    {
        await PickupSchemaUpgrade.ApplyAsync(source, target, ct);
        await using var db = RentalDataMigration.Rental(target);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migrations/rental/004_return_timeline.sql"), ct);
        var checksum = RentalDataMigration.Hash(sql.Replace("\r\n", "\n").TrimEnd() + "\n");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-return-schema'))", ct);
        var existing = await db.Database.SqlQuery<string>($"""SELECT checksum AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=4 AND service_name='RentalService'""").ToListAsync(ct);
        var count = await db.Database.SqlQuery<int>($"""SELECT count(*)::integer AS "Value" FROM "__FleetFlowServiceSchema" WHERE version=4""").SingleAsync(ct);
        if (count != existing.Count || existing.Count > 0 && existing.Single() != checksum) throw new InvalidOperationException("Return schema ownership or checksum differs.");
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "__FleetFlowServiceSchema"(version,service_name,checksum) VALUES(4,'RentalService',{checksum}) ON CONFLICT(version) DO NOTHING""", ct);
        await tx.CommitAsync(ct);
    }
}
