using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;
namespace RentalService.Api.Services;
public sealed record RentalActivation(bool Active,Guid MigrationId)
{
 public static RentalActivation Read(IConfiguration config)
 {
  var active=config.GetValue<bool>("RENTAL_SERVICE_ACTIVE");Guid.TryParse(config["RENTAL_MIGRATION_ID"],out var id);
  if(active&&(id==Guid.Empty||!(config.GetValue<bool?>("SERVICE_AUTH_ENABLED")??config.GetValue<bool>("ServiceAuth:Enabled"))))
   throw new InvalidOperationException("Rental activation requires a verified migration ID and internal authentication.");
  return new(active,id);
 }
}
public sealed class RentalReadiness(OwnedDatabase database,RentalActivation activation):IDatabaseReadiness
{
 public async Task<bool> IsReadyAsync(CancellationToken ct)
 {
  if(!activation.Active)return false;
  try{
   await using var db=new NpgsqlConnection(database.ConnectionString);await db.OpenAsync(ct);
   await using var cmd=new NpgsqlCommand("""SELECT EXISTS(SELECT 1 FROM "__FleetFlowServiceSchema" s,"RentalMigrationReceipts" m WHERE s.version=3 AND s.service_name='RentalService' AND m."Id"=@id AND m."Verified"=true)""",db);
   cmd.Parameters.AddWithValue("id",activation.MigrationId);return (bool)(await cmd.ExecuteScalarAsync(ct))!;
  }catch(Exception ex)when(ex is NpgsqlException or InvalidOperationException or TimeoutException){return false;}
 }
}
public sealed class RentalActivationFilter(RentalActivation activation,IDatabaseReadiness ready):IAsyncResourceFilter
{
 public async Task OnResourceExecutionAsync(ResourceExecutingContext context,ResourceExecutionDelegate next)
 {
  if(!activation.Active||!await ready.IsReadyAsync(context.HttpContext.RequestAborted))
  {context.Result=new ObjectResult(new{message="Rental service is not ready for booking requests."}){StatusCode=503};return;}
  await next();
 }
}
