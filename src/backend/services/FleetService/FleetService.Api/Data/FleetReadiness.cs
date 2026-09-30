using Microsoft.EntityFrameworkCore;

namespace FleetService.Api.Data;

public sealed class FleetReadiness
{
    private int initialized;
    public void MarkInitialized() => Interlocked.Exchange(ref initialized, 1);

    public async Task<IResult> CheckAsync(FleetDbContext db, ILogger<FleetReadiness> logger, CancellationToken ct = default)
    {
        if (Volatile.Read(ref initialized) == 0)
            return Results.Json(new { status = "NotReady", ready = false, message = "Database initialization is incomplete." }, statusCode: 503);

        try
        {
            if (await db.Database.CanConnectAsync(ct))
                return Results.Ok(new { status = "Healthy", database = "Connected", ready = true });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "FleetService readiness database check failed.");
        }

        return Results.Json(new { status = "NotReady", ready = false, message = "Database is unavailable." }, statusCode: 503);
    }
}
