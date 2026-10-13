using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
namespace FleetService.Api.Services;
public sealed record RentalRouting(bool Remote,Uri BaseUrl,Guid MigrationId)
{
 public static RentalRouting Read(IConfiguration config,IHostEnvironment environment)
 {
  var mode=config["RENTAL_OWNERSHIP"]??"Legacy";
  if(mode is not ("Legacy" or "Remote"))throw new InvalidOperationException("RENTAL_OWNERSHIP must be Legacy or Remote.");
  var raw=config["INTERNAL_RENTAL_URL"]??"http://127.0.0.1:5003/";
  var url=FleetFlow.ServiceDefaults.InternalEndpoint.Read(raw,"rental-service",environment);
  Guid.TryParse(config["RENTAL_MIGRATION_ID"],out var id);
  if(mode=="Remote" && (id==Guid.Empty || !(config.GetValue<bool?>("SERVICE_AUTH_ENABLED")??config.GetValue<bool>("ServiceAuth:Enabled"))))
   throw new InvalidOperationException("Remote Rental ownership requires a verified migration ID and internal authentication.");
  return new(mode=="Remote",url,id);
 }
 public async Task<bool> ReadyAsync(FleetDbContext db,CancellationToken ct=default)
 {
  if(!Remote)return false;
  if(!db.Database.IsNpgsql())return false;
  try{return await db.Database.SqlQuery<int>($"""SELECT 1 AS "Value" FROM "RentalLedgerMigrationReceipts" WHERE "Id"={MigrationId} AND "Verified"=true""").AnyAsync(ct);}
  catch(Exception ex)when(ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException){return false;}
 }
}
