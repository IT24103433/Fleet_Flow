using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RentalMigration;
using Npgsql;
using ServiceDatabaseTool;
try
{
    var options=new Dictionary<string,string>(StringComparer.Ordinal);
    foreach(var pair in args.Chunk(2))
    {
        if(pair.Length!=2 || !new[]{"--mode","--file","--sha256","--backup","--write-pause","--apply"}.Contains(pair[0]) || !options.TryAdd(pair[0],pair[1]))
            throw new ArgumentException("Use --mode export|import|export-target|reconcile|upgrade-pickup|upgrade-rentals --file path [--sha256 hash --backup receipt --write-pause confirmed --apply true]. Default import is dry run.");
    }
    if(!options.TryGetValue("--mode",out var mode)||!options.TryGetValue("--file",out var file))throw new ArgumentException("Mode and file are required.");
    var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var settings=new NpgsqlConnectionStringBuilder {Host=config["DB_HOST"]??"localhost",Port=config.GetValue<int?>("DB_PORT")??5432,
        Username=config["DB_USER"]??"postgres",Password=config["DB_PASSWORD"]??"",Database=config["RENTAL_MIGRATION_SOURCE_DB"]??"fleetflow_fleet",Timeout=5,CommandTimeout=30};
    var legacy=settings.ConnectionString;settings.Database=config["RENTAL_MIGRATION_TARGET_DB"]??"fleetflow_rental";var target=settings.ConnectionString;
    var targetName=settings.Database;
    if(mode is "export" or "export-target")
    {
        var json=await RentalDataMigration.ExportAsync(mode=="export"?legacy:target,mode=="export");
        if(File.Exists(file))throw new InvalidOperationException("Export destination exists; choose a new filename.");
        await File.WriteAllTextAsync(file,json);Console.WriteLine(JsonSerializer.Serialize(new{sha256=RentalDataMigration.Hash(json),file=Path.GetFullPath(file)}));return 0;
    }
    if(mode is not ("import" or "reconcile" or "upgrade-pickup" or "upgrade-rentals"))throw new ArgumentException("Invalid mode.");
    var payload=await File.ReadAllTextAsync(file);var hash=options.GetValueOrDefault("--sha256")??"";
    RentalDataMigration.Validate(payload,hash);
    var apply=options.GetValueOrDefault("--apply")=="true";
    if(apply)
    {
        if(options.GetValueOrDefault("--write-pause")!="confirmed")throw new InvalidOperationException("Writes require an explicit controlled write pause.");
        using var backup=JsonDocument.Parse(await File.ReadAllTextAsync(options.GetValueOrDefault("--backup")??throw new InvalidOperationException("A verified backup receipt is required.")));
        if(!backup.RootElement.GetProperty("verified").GetBoolean())throw new InvalidOperationException("Backup is unverified.");
        foreach(var database in mode is "reconcile" or "upgrade-pickup" or "upgrade-rentals"?new[]{"fleetflow_auth","fleetflow_fleet","fleetflow_rental"}:new[]{"fleetflow_auth","fleetflow_fleet"})
        {
            var item=backup.RootElement.GetProperty("databases").GetProperty(database);
            if(!item.GetProperty("restoredAndVerified").GetBoolean())throw new InvalidOperationException("Backup restoration is unverified.");
            var bytes=await File.ReadAllBytesAsync(item.GetProperty("dump").GetString()!);
            var actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            if(actual!=item.GetProperty("sha256").GetString())throw new InvalidOperationException("Backup archive hash differs.");
        }
        var uploads=backup.RootElement.GetProperty("uploads");
        if(!uploads.GetProperty("verified").GetBoolean())throw new InvalidOperationException("Uploads backup is unverified.");
        var uploadBytes=await File.ReadAllBytesAsync(uploads.GetProperty("archive").GetString()!);
        if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(uploadBytes)).ToLowerInvariant()!=uploads.GetProperty("sha256").GetString())
            throw new InvalidOperationException("Upload archive hash differs.");
    }
    if(mode is "upgrade-pickup" or "upgrade-rentals")
    {
        if(await RentalDataMigration.ExportAsync(target,false)!=RentalDataMigration.Canonical(RentalDataMigration.Validate(payload,hash)))throw new InvalidOperationException("Rental changed after export.");
        if(apply)await ReturnSchemaUpgrade.ApplyAsync(legacy,target);
        Console.WriteLine(JsonSerializer.Serialize(new{upgraded=apply,schemaVersion=4}));return 0;
    }
    if(mode=="reconcile")
    {Console.WriteLine(JsonSerializer.Serialize(new{reconciled=apply?await RentalDataMigration.ReconcileLegacyAsync(legacy,target,payload,hash):0,dryRun=!apply}));return 0;}
    if(apply)
    {
        settings.Database="postgres";
        var foundation=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/rental/001_foundation.sql"));
        await FoundationMigration.RunAsync(settings.ConnectionString,"rental",targetName!,foundation,true);
    }
    var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/rental/002_bookings.sql"));
    Console.WriteLine(JsonSerializer.Serialize(await RentalDataMigration.ImportAsync(legacy,target,payload,hash,sql,apply),new JsonSerializerOptions{WriteIndented=true}));return 0;
}
catch(PostgresException ex)
{Console.Error.WriteLine($"Migration database error {ex.SqlState}; table={ex.TableName??"unknown"}, column={ex.ColumnName??"unknown"}, constraint={ex.ConstraintName??"unknown"}. No credentials or record values are logged.");return 1;}
catch(Exception ex)when(ex is NpgsqlException or IOException or JsonException or KeyNotFoundException)
{Console.Error.WriteLine($"Migration failed ({ex.GetType().Name}). Verify the local connection, export and backup receipt. No credentials are logged.");return 1;}
catch(Exception ex)when(ex is ArgumentException or InvalidOperationException)
{Console.Error.WriteLine(ex.Message);return 2;}
