using RentalService.Api.Services;
namespace RentalService.Api.Middleware;
public sealed class BookingErrorsMiddleware(RequestDelegate next,ILogger<BookingErrorsMiddleware> logger)
{
 public async Task InvokeAsync(HttpContext context)
 {
  try{await next(context);}
  catch(BookingRequestException ex){context.Response.StatusCode=ex.Status;await context.Response.WriteAsJsonAsync(new{message=ex.Message});}
  catch(Exception ex)when(ex is Npgsql.NpgsqlException or Microsoft.EntityFrameworkCore.DbUpdateException)
  {
   logger.LogWarning(ex,"Booking persistence unavailable; pending recovery remains durable.");
   context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new{message="Booking service is temporarily unavailable. Check My bookings before retrying."});
  }
 }
}
