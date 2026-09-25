using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IdentityService.Api.Middleware;

public class ForcedPasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public ForcedPasswordChangeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var isPasswordChangeActive = context.User.Identity?.IsAuthenticated == true &&
            context.User.Claims.Any(c => c.Type == "must_change_password" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

        if (isPasswordChangeActive)
        {
            var isAllowedPath = path.StartsWith("/api/auth/change-password") ||
                                path.StartsWith("/api/auth/login") ||
                                path.StartsWith("/api/auth/register") ||
                                path == "/" ||
                                path.StartsWith("/health") ||
                                path.StartsWith("/swagger") ||
                                path.StartsWith("/openapi") ||
                                path.StartsWith("/uploads");

            if (!isAllowedPath)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Password change required before accessing protected application resources.",
                    mustChangePassword = true
                });
                return;
            }
        }

        await _next(context);
    }
}
