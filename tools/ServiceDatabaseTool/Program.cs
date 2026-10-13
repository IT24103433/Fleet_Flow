using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ServiceDatabaseTool;

try
{
    var serviceIndex = Array.IndexOf(args, "--service");
    var databaseIndex = Array.IndexOf(args, "--database");
    var allowed = new HashSet<string> { "--service", "--database", "--apply" };
    if (serviceIndex < 0 || serviceIndex + 1 >= args.Length ||
        args.Where(value => value.StartsWith("--", StringComparison.Ordinal)).Any(value => !allowed.Contains(value)))
        throw new ArgumentException("Usage: --service rental|notifications [--database owned-name] [--apply]. Default is a read-only dry run.");
    var service = args[serviceIndex + 1];
    var descriptor = FoundationMigration.Descriptor(service);
    if (databaseIndex >= 0 && databaseIndex + 1 >= args.Length) throw new ArgumentException("A database name is required.");
    var database = databaseIndex >= 0 ? args[databaseIndex + 1] : descriptor.Database;
    FoundationMigration.ValidateDatabase(service, database);
    var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var connection = config["MIGRATION_ADMIN_CONNECTION"] ?? new NpgsqlConnectionStringBuilder
    {
        Host = config["DB_HOST"] ?? "localhost", Port = config.GetValue<int?>("DB_PORT") ?? 5432,
        Database = "postgres", Username = config["DB_USER"] ?? "postgres", Password = config["DB_PASSWORD"] ?? "",
        Timeout = 5, CommandTimeout = 10
    }.ConnectionString;
    var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migrations", service, "001_foundation.sql"));
    var result = await FoundationMigration.RunAsync(connection, service, database, sql, args.Contains("--apply"));
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception) when (exception is NpgsqlException or IOException)
{
    Console.Error.WriteLine("Local database initialization failed. Verify the configured connection and migration files. No connection credentials are logged.");
    return 1;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
