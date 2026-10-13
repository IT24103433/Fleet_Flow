using FleetService.Api.Services;
namespace FleetService.Api.Middleware;
public sealed class RentalAvailabilityMiddleware(RequestDelegate next)
{
 public async Task InvokeAsync(HttpContext context)
 {try{await next(context);}catch(RentalUnavailableException){context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new{message="Rental service is temporarily unavailable. Please try again."});}}
}
