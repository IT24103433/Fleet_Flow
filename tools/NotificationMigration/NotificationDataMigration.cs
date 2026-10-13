using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NotificationService.Api.Data;
namespace NotificationMigration;
public sealed record MigrationResult(Guid MigrationId,int RecordCount,string SourceHash,bool Applied);
public static class NotificationDataMigration
{
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static string Canonical(IEnumerable<InboxNotification> rows)=>JsonSerializer.Serialize(rows.OrderBy(r=>r.Id));
    public static void Guard(string connection,bool legacy)
    {
        var c=new NpgsqlConnectionStringBuilder(connection);
        if(c.Host is not ("localhost" or "127.0.0.1" or "::1") || c.Database is null ||
            (legacy?c.Database!="fleetflow_fleet" && !c.Database.StartsWith("fleetflow_hardening_",StringComparison.Ordinal):
                c.Database!="fleetflow_notifications" && !c.Database.StartsWith("fleetflow_notifications_foundation_test_",StringComparison.Ordinal)))
            throw new InvalidOperationException("Notification migration is restricted to explicitly named loopback databases.");
    }
    private static NotificationDbContext Context(string connection)=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options);
    public static async Task<string> ExportAsync(string connection,bool legacy,CancellationToken ct=default)
    {
        Guard(connection,legacy);await using var db=Context(connection);
        return Canonical(await db.Notifications.AsNoTracking().ToListAsync(ct));
    }
    public static IReadOnlyList<InboxNotification> Validate(string json,string expectedHash)
    {
        if(expectedHash.Length!=64 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(json)),Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant())))
            throw new InvalidOperationException("Export hash mismatch; refusing data changes.");
        var rows=JsonSerializer.Deserialize<InboxNotification[]>(json)??throw new InvalidOperationException("Invalid export.");
        if(rows.Select(r=>r.Id).Distinct().Count()!=rows.Length || rows.Select(r=>(r.EventId,r.TargetUserId)).Distinct().Count()!=rows.Length ||
            rows.Any(r=>r.Id==Guid.Empty||r.EventId==Guid.Empty||r.TargetUserId==Guid.Empty||r.CreatedAt.Kind!=DateTimeKind.Utc||
                string.IsNullOrWhiteSpace(r.Category)||r.Category.Length>50||string.IsNullOrWhiteSpace(r.Title)||r.Title.Length>200||
                string.IsNullOrWhiteSpace(r.Message)||r.Message.Length>2000))
            throw new InvalidOperationException("Export identifiers, timestamps, uniqueness or field lengths are invalid.");
        return rows;
    }
    public static async Task<MigrationResult> ImportAsync(string connection,string json,string expectedHash,string sql,bool apply,CancellationToken ct=default)
    {
        Guard(connection,false);var rows=Validate(json,expectedHash);
        if(!apply)return new(Guid.Empty,rows.Count,expectedHash,false);
        var checksum=Hash(sql.Replace("\r\n","\n").TrimEnd()+"\n");
        await using var db=Context(connection);await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fleetflow-notification-import'))",ct);
        await using(var marker=new NpgsqlCommand("SELECT checksum,service_name FROM \"__FleetFlowServiceSchema\" WHERE version=2",(NpgsqlConnection)db.Database.GetDbConnection(),(NpgsqlTransaction)tx.GetDbTransaction()))
        {
            await using var reader=await marker.ExecuteReaderAsync(ct);
            if(await reader.ReadAsync(ct) && (reader.GetString(0)!=checksum||reader.GetString(1)!="NotificationService"))
                throw new InvalidOperationException("Notification schema checksum or owner differs.");
        }
        await using(var owner=new NpgsqlCommand("SELECT service_name FROM \"__FleetFlowServiceSchema\" WHERE version=1",(NpgsqlConnection)db.Database.GetDbConnection(),(NpgsqlTransaction)tx.GetDbTransaction()))
            if((string?)await owner.ExecuteScalarAsync(ct)!="NotificationService")throw new InvalidOperationException("Foundation belongs to another service.");
        await db.Database.ExecuteSqlRawAsync(sql,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__FleetFlowServiceSchema\" (version,service_name,checksum) VALUES (2,'NotificationService',{checksum}) ON CONFLICT (version) DO NOTHING",ct);
        var current=await db.Notifications.AsNoTracking().ToListAsync(ct);
        await using(var receipt=new NpgsqlCommand("SELECT \"Id\",\"SourceHash\" FROM \"NotificationMigrationReceipts\" WHERE \"Verified\"=true",(NpgsqlConnection)db.Database.GetDbConnection(),(NpgsqlTransaction)tx.GetDbTransaction()))
        {
            await using var reader=await receipt.ExecuteReaderAsync(ct);
            if(await reader.ReadAsync(ct))
            {
                if(reader.GetString(1)!=expectedHash || Canonical(current)!=Canonical(rows))
                    throw new InvalidOperationException("A verified migration or later business writes already exist; reconcile under a write pause instead.");
                return new(reader.GetGuid(0),rows.Count,expectedHash,false);
            }
        }
        if(current.Count!=0 && Canonical(current)!=Canonical(rows))throw new InvalidOperationException("Target already contains different inbox data; refusing overwrite.");
        if(current.Count==0 && rows.Count!=0){db.Notifications.AddRange(rows);await db.SaveChangesAsync(ct);}
        if(Canonical(await db.Notifications.AsNoTracking().ToListAsync(ct))!=Canonical(rows))throw new InvalidOperationException("Imported row checksum differs; transaction rolled back.");
        var id=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"NotificationMigrationReceipts\" (\"Id\",\"SourceHash\",\"RecordCount\",\"Verified\") VALUES ({id},{expectedHash},{rows.Count},true)",ct);
        await tx.CommitAsync(ct);return new(id,rows.Count,expectedHash,true);
    }
    public static async Task<int> ReconcileLegacyAsync(string connection,string json,string expectedHash,CancellationToken ct=default)
    {
        Guard(connection,true);var rows=Validate(json,expectedHash);await using var db=Context(connection);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var row in rows)
        {
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Notifications" AS original ("Id","EventId","TargetUserId","Category","Title","Message","RelatedEntityId","VehicleId","CreatedAt","IsRead")
                VALUES ({row.Id},{row.EventId},{row.TargetUserId},{row.Category},{row.Title},{row.Message},{row.RelatedEntityId},{row.VehicleId},{row.CreatedAt},{row.IsRead})
                ON CONFLICT ("Id") DO UPDATE SET "IsRead"=original."IsRead" OR EXCLUDED."IsRead"
                WHERE original."EventId"=EXCLUDED."EventId" AND original."TargetUserId"=EXCLUDED."TargetUserId"
                    AND original."Category"=EXCLUDED."Category" AND original."Title"=EXCLUDED."Title" AND original."Message"=EXCLUDED."Message"
                    AND original."RelatedEntityId" IS NOT DISTINCT FROM EXCLUDED."RelatedEntityId"
                    AND original."VehicleId" IS NOT DISTINCT FROM EXCLUDED."VehicleId" AND original."CreatedAt"=EXCLUDED."CreatedAt"
                """,ct);
            if(changed!=1)throw new InvalidOperationException("Legacy record differs; reconciliation rolled back without overwriting history.");
        }
        await tx.CommitAsync(ct);return rows.Count;
    }
}
