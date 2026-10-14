using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace FleetFlow.ServiceDefaults;
public sealed class ApiErrorsMiddleware(RequestDelegate next, ILogger<ApiErrorsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (!context.Response.HasStarted && !(ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested))
        {
            var status = ex switch { BadHttpRequestException bad => bad.StatusCode, DbException or TimeoutException => 503, _ => 500 };
            logger.LogWarning("Request failed with {ExceptionType}; trace {TraceId}.", ex.GetType().Name, context.TraceIdentifier);
            context.Response.StatusCode = status; context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { title = status < 500 ? "Invalid request" : "Request could not be completed", status,
                message = status < 500 ? "Check the request format and supported size." : "The service could not complete this request. Refresh its current state before trying again.", traceId = context.TraceIdentifier },
                options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
