using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FleetFlow.ServiceDefaults;
using Npgsql;

namespace ServiceDatabaseTool;

public sealed record MigrationReport(string Service, string Database, bool DatabaseExists,
    bool SchemaInitialized, bool Applied, string Checksum);

public static class FoundationMigration
{
    public static OwnedServiceDescriptor Descriptor(string service) => service switch
    {
        "rental" => new("RentalService", "fleetflow_rental", "RENTAL"),
        "notifications" => new("NotificationService", "fleetflow_notifications", "NOTIFICATION"),
        _ => throw new ArgumentException("Service must be rental or notifications.")
    };

    public static string ValidateDatabase(string service, string database)
    {
        var descriptor = Descriptor(service);
        if (!Regex.IsMatch(database, "^[a-z0-9_]+$", RegexOptions.CultureInvariant) ||
            (database != descriptor.Database && !database.StartsWith(descriptor.Database + "_foundation_test_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Only the selected service's new owned database may be initialized.");
        return database;
    }

    public static async Task<MigrationReport> RunAsync(string adminConnection, string service,
        string database, string sql, bool apply, CancellationToken cancellationToken = default)
    {
        var descriptor = Descriptor(service);
        ValidateDatabase(service, database);
        var adminSettings = new NpgsqlConnectionStringBuilder(adminConnection);
        if (adminSettings.Host is not ("localhost" or "127.0.0.1" or "::1") || adminSettings.Database != "postgres")
            throw new InvalidOperationException("Foundation initialization is restricted to a loopback PostgreSQL admin connection targeting postgres.");
        sql = sql.Replace("\r\n", "\n").TrimEnd() + "\n";
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
        await using var admin = new NpgsqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync(cancellationToken);
        await using var existsCommand = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @database)", admin);
        existsCommand.Parameters.AddWithValue("database", database);
        var exists = (bool)(await existsCommand.ExecuteScalarAsync(cancellationToken))!;
        if (!exists && !apply) return new(descriptor.Name, database, false, false, false, checksum);
        if (!exists)
        {
            try
            {
                // The identifier was restricted to lower-case letters, digits and underscores.
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.DuplicateDatabase) { }
        }
        var ownedSettings = new NpgsqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = database };
        await using var owned = new NpgsqlConnection(ownedSettings.ConnectionString);
        await owned.OpenAsync(cancellationToken);
        await using var transaction = await owned.BeginTransactionAsync(cancellationToken);
        if (apply)
        {
            await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@service))", owned, transaction);
            gate.Parameters.AddWithValue("service", "fleetflow-foundation:" + descriptor.Name);
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var tablesCommand = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname = 'public'", owned, transaction);
        var tables = new List<string>();
        await using (var reader = await tablesCommand.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
        if (tables.Contains("__FleetFlowServiceSchema"))
        {
            await using var verify = new NpgsqlCommand("SELECT service_name, checksum FROM \"__FleetFlowServiceSchema\" WHERE version = 1", owned, transaction);
            await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != descriptor.Name || reader.GetString(1) != checksum)
                throw new InvalidOperationException("Existing foundation ownership or migration checksum does not match; refusing changes.");
            return new(descriptor.Name, database, true, true, false, checksum);
        }
        if (tables.Count != 0)
            throw new InvalidOperationException("The target database contains unrecognized tables; refusing initialization.");
        if (!apply) return new(descriptor.Name, database, true, false, false, checksum);
        await using (var initialize = new NpgsqlCommand(sql, owned, transaction))
            await initialize.ExecuteNonQueryAsync(cancellationToken);
        await using (var record = new NpgsqlCommand("INSERT INTO \"__FleetFlowServiceSchema\" (version, service_name, checksum) VALUES (1,@service,@checksum)", owned, transaction))
        {
            record.Parameters.AddWithValue("service", descriptor.Name);
            record.Parameters.AddWithValue("checksum", checksum);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(descriptor.Name, database, true, true, true, checksum);
    }
}
