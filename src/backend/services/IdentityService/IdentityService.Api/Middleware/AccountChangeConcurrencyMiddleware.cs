using Microsoft.EntityFrameworkCore;
namespace IdentityService.Api.Middleware;
public sealed class AccountChangeConcurrencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (DbUpdateConcurrencyException) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 409; context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { message = "This account changed during your request. Refresh its current state before trying again." }, context.RequestAborted);
        }
    }
}
