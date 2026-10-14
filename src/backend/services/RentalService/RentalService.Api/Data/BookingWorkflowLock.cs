using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace RentalService.Api.Data;
public static class BookingWorkflowLock
{
 public static async Task<IDbContextTransaction?> BeginAsync(RentalDbContext db,Guid id,CancellationToken ct)
 {
  if(!db.Database.IsRelational())return null;
  var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,ct);
  try{await db.Workflows.FromSqlInterpolated($"""SELECT * FROM "BookingWorkflows" WHERE "Id"={id} FOR UPDATE""").AsNoTracking().ToListAsync(ct);return tx;}
  catch{await tx.DisposeAsync();throw;}
 }
}
