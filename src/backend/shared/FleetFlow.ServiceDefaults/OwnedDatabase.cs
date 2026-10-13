using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FleetFlow.ServiceDefaults;

public sealed record OwnedServiceDescriptor(string Name, string Database, string ConfigurationPrefix);
public sealed record OwnedDatabase(string ConnectionString, OwnedServiceDescriptor Service)
{
    public static OwnedDatabase Read(IConfiguration config, OwnedServiceDescriptor service)
    {
        var database = config[$"{service.ConfigurationPrefix}_DB_NAME"] ?? service.Database;
        if (!Regex.IsMatch(database, "^[a-z0-9_]+$", RegexOptions.CultureInvariant) ||
            (database != service.Database && !database.StartsWith(service.Database + "_foundation_test_", StringComparison.Ordinal)))
            throw new InvalidOperationException($"{service.Name} may connect only to its owned database.");
        var raw = config.GetConnectionString("DefaultConnection");
        var connection = string.IsNullOrWhiteSpace(raw) ? new NpgsqlConnectionStringBuilder
        {
            Host = config["DB_HOST"] ?? "localhost", Port = config.GetValue<int?>("DB_PORT") ?? 5432,
            Database = database,
            Username = config[$"{service.ConfigurationPrefix}_DB_USER"] ?? config["DB_USER"] ?? "postgres",
            Password = config[$"{service.ConfigurationPrefix}_DB_PASSWORD"] ?? config["DB_PASSWORD"] ?? "",
            Timeout = 5, CommandTimeout = 5
        } : new NpgsqlConnectionStringBuilder(raw);
        if (connection.Database != database)
            throw new InvalidOperationException($"{service.Name} connection string targets a different database.");
        if (database != service.Database && connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Verification databases require a loopback connection.");
        return new(connection.ConnectionString, service with { Database = database });
    }
}

public interface IDatabaseReadiness
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseReadiness(OwnedDatabase database) : IDatabaseReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT service_name FROM \"__FleetFlowServiceSchema\" WHERE version = 1", connection);
            return (string?)await command.ExecuteScalarAsync(cancellationToken) == database.Service.Name;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            // Never disclose credentials, connection strings or SQL exceptions in health responses.
            return false;
        }
    }
}
