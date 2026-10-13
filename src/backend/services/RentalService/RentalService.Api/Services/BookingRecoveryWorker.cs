using FleetFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
namespace RentalService.Api.Services;
public sealed class BookingRecoveryWorker(IServiceScopeFactory scopes,RentalActivation activation,IDatabaseReadiness ready,ILogger<BookingRecoveryWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct)
 {
  while(!ct.IsCancellationRequested)
  {
   try{
    if(activation.Active&&await ready.IsReadyAsync(ct))
    {
     using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<RentalDbContext>();
     var ids=await db.Workflows.AsNoTracking().Where(w=>(w.Phase=="ReserveRequested"||w.Phase=="CancelRequested")&&w.NextAttemptAt<=DateTime.UtcNow).OrderBy(w=>w.CreatedAt).Select(w=>w.Id).Take(20).ToListAsync(ct);
     foreach(var id in ids)
      try{await scope.ServiceProvider.GetRequiredService<BookingCoordinator>().ResumeAsync(id,ct);}
      catch(BookingRequestException){db.ChangeTracker.Clear();}
    }
   }catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
   catch(Exception ex){logger.LogWarning(ex,"Booking recovery deferred; durable workflows remain pending.");}
   try{await Task.Delay(1000,ct);}catch(OperationCanceledException){break;}
  }
 }
}
