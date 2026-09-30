using Microsoft.AspNetCore.Authorization;

namespace FleetService.Api.Middleware;

public class ForcedPasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var isProtected = endpoint?.Metadata.GetMetadata<IAuthorizeData>() != null &&
                          endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null;
        if (isProtected && context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(c => c.Type == "must_change_password" &&
                c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Password change required before accessing protected application resources.",
                mustChangePassword = true
            });
            return;
        }

        await next(context);
    }
}
