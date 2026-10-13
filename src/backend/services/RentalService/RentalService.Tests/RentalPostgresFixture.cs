using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RentalMigration;
using RentalService.Api.Data;
using ServiceDatabaseTool;
namespace RentalService.Tests;
public sealed class RentalPostgresFactAttribute:FactAttribute
{public RentalPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))Skip="Requires isolated loopback PostgreSQL.";}}
public sealed class RentalPostgresFixture:IAsyncDisposable
{
 public string Source{get;private set;}="";public string Target{get;private set;}="";private string admin="";
 public string SourceName{get;private set;}="";public string TargetName{get;private set;}="";
 public Guid VehicleId{get;}=Guid.NewGuid();public Guid Customer{get;}=Guid.NewGuid();public Guid MigrationId{get;private set;}
 public static Task<string> Schema()=>File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/rental/002_bookings.sql"));
 public static async Task<RentalPostgresFixture> Create(bool migrate=true)
 {
  var fixture=new RentalPostgresFixture();var settings=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
  if(settings.Host is not ("localhost" or "127.0.0.1")||settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)throw new InvalidOperationException("Unsafe test connection.");
  settings.Database="postgres";fixture.admin=settings.ConnectionString;
  var suffix=Guid.NewGuid().ToString("N")[..12];fixture.SourceName="fleetflow_hardening_rental_"+suffix;fixture.TargetName="fleetflow_rental_foundation_test_"+suffix;
  await using(var db=new NpgsqlConnection(fixture.admin)){await db.OpenAsync();await using var create=new NpgsqlCommand($"CREATE DATABASE {fixture.SourceName}",db);await create.ExecuteNonQueryAsync();}
  fixture.Source=new NpgsqlConnectionStringBuilder(fixture.admin){Database=fixture.SourceName}.ConnectionString;
  var foundation=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/rental/001_foundation.sql"));
  await FoundationMigration.RunAsync(fixture.admin,"rental",fixture.TargetName,foundation,true);
  fixture.Target=new NpgsqlConnectionStringBuilder(fixture.admin){Database=fixture.TargetName}.ConnectionString;
  try
  {
  await using(var fleet=RentalDataMigration.Fleet(fixture.Source))
  {
   await fleet.Database.EnsureCreatedAsync();var cat=new VehicleCategory{Id=Guid.NewGuid(),Name="Sedan",Description="Test"};fleet.VehicleCategories.Add(cat);
   fleet.Vehicles.Add(new(){Id=fixture.VehicleId,Vin="TESTRENTAL1234567",LicensePlate="TEST-R",Make="Toyota",Model="Corolla",Year=2019,VehicleCategoryId=cat.Id,DailyRate=9000,Transmission="Automatic",FuelType="Hybrid",SeatingCapacity="5",HubLocation="Colombo",Status=VehicleStatus.Available});await fleet.SaveChangesAsync();
  }
  if(migrate){var json=await RentalDataMigration.ExportAsync(fixture.Source,true);var receipt=await RentalDataMigration.ImportAsync(fixture.Source,fixture.Target,json,RentalDataMigration.Hash(json),await Schema(),true);fixture.MigrationId=receipt.MigrationId;}
  return fixture;
  }catch{await fixture.DisposeAsync();throw;}
 }
 public FleetDbContext Fleet(bool remote=true)=>new(new DbContextOptionsBuilder<FleetDbContext>().UseNpgsql(Source).Options,remote?new RentalRouting(true,new Uri("http://127.0.0.1:5003/"),MigrationId):null);
 public RentalDbContext Rental()=>RentalDataMigration.Rental(Target);
 public async ValueTask DisposeAsync()
 {
  foreach(var (name,connection) in new[]{(TargetName,Target),(SourceName,Source)})
  {
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^(fleetflow_hardening_rental_|fleetflow_rental_foundation_test_)[a-f0-9]{12}$"))throw new InvalidOperationException("Unsafe cleanup target.");
   using(var pool=new NpgsqlConnection(connection))NpgsqlConnection.ClearPool(pool);
   await using var db=new NpgsqlConnection(admin);await db.OpenAsync();await using var drop=new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)",db);await drop.ExecuteNonQueryAsync();
  }
 }
}
