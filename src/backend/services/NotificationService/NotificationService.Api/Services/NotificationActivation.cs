using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;
namespace NotificationService.Api.Services;
public sealed record NotificationActivation(bool Active,Guid MigrationId)
{
    public static NotificationActivation Read(IConfiguration config)
    {
        var active=config.GetValue<bool>("NOTIFICATION_SERVICE_ACTIVE");
        Guid.TryParse(config["NOTIFICATION_MIGRATION_ID"],out var id);
        if(active&&(id==Guid.Empty || !(config.GetValue<bool?>("SERVICE_AUTH_ENABLED")??config.GetValue<bool>("ServiceAuth:Enabled"))))
            throw new InvalidOperationException("Notification activation requires a verified migration ID and internal service authentication.");
        return new(active,id);
    }
}
public sealed class NotificationReadiness(OwnedDatabase database,NotificationActivation activation):IDatabaseReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if(!activation.Active)return false;
        try
        {
            await using var db=new NpgsqlConnection(database.ConnectionString);await db.OpenAsync(ct);
            await using var command=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"__FleetFlowServiceSchema\" s, \"NotificationMigrationReceipts\" m WHERE s.version=2 AND s.service_name='NotificationService' AND m.\"Id\"=@id AND m.\"Verified\"=true)",db);
            command.Parameters.AddWithValue("id",activation.MigrationId);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }
        catch(Exception ex)when(ex is NpgsqlException or InvalidOperationException or TimeoutException){return false;}
    }
}
public sealed class NotificationActivationFilter(NotificationActivation activation,IDatabaseReadiness ready):IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context,ResourceExecutionDelegate next)
    {
        if(!activation.Active || !await ready.IsReadyAsync(context.HttpContext.RequestAborted))
        {context.Result=new ObjectResult(new{message="Notification service is not ready for business requests."}){StatusCode=503};return;}
        await next();
    }
}
