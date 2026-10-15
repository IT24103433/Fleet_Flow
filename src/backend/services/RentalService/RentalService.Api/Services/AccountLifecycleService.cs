using FleetFlow.Contracts;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;

namespace RentalService.Api.Services;

public sealed class AccountLifecycleService(RentalDbContext db,IConfiguration configuration)
{
    public async Task<bool> Outstanding(Guid customer,CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        return await db.Bookings.AnyAsync(x=>x.CustomerId==customer&&(x.Status==BookingStatus.Pending||x.Status==BookingStatus.Confirmed)&&x.EndDateTime>now,ct)
            ||await db.Workflows.AnyAsync(x=>x.CustomerId==customer&&x.Phase!="Done"&&x.Phase!="Rejected",ct)
            ||await db.Pickups.AnyAsync(x=>x.State!=RentalLifecycleState.Completed&&db.Bookings.Any(b=>b.Id==x.BookingId&&b.CustomerId==customer),ct);
    }
    public async Task<object> Freeze(Guid customer,AccountLifecycleCommand input,CancellationToken ct)
    {
        if(!configuration.GetValue<bool>("REQUIRE_DRIVER_VERIFICATION"))throw new BookingRequestException(503,"Account lifecycle policy is not activated consistently.");
        if(input.OperationId==Guid.Empty)throw new BookingRequestException(400,"An account operation reference is required.");
        await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({customer.ToString()},0))",ct);
        var guard=await db.AccountGuards.SingleOrDefaultAsync(x=>x.CustomerId==customer,ct);
        if(guard?.State=="Purged")throw new BookingRequestException(409,"An anonymised account cannot be reactivated.");
        // Identity accepts a replacement command only after a fresh owner OTP; it serialises the owner's challenges.
        // Replacing an interrupted freeze keeps bookings blocked and lets an expired challenge recover safely.
        if(await Outstanding(customer,ct))throw new BookingRequestException(409,"Resolve future bookings or unfinished rentals before deleting your account.");
        if(guard==null){guard=new(){CustomerId=customer,OperationId=input.OperationId};db.AccountGuards.Add(guard);}
        else{guard.State="Frozen";guard.OperationId=input.OperationId;}
        await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return new{success=true};
    }
    public async Task<object> Release(Guid customer,AccountLifecycleCommand input,CancellationToken ct)
    {
        await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({customer.ToString()},0))",ct);
        var guard=await db.AccountGuards.SingleOrDefaultAsync(x=>x.CustomerId==customer,ct);
        if(guard!=null)
        {
            if(guard.OperationId!=input.OperationId||guard.State=="Purged")throw new BookingRequestException(409,"The account cannot be restored from this operation.");
            guard.State="Released";await db.SaveChangesAsync(ct);
        }
        if(tx!=null)await tx.CommitAsync(ct);return new{success=true};
    }
    public async Task<object> Anonymize(Guid customer,AccountLifecycleCommand input,CancellationToken ct)
    {
        await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({customer.ToString()},0))",ct);
        var guard=await db.AccountGuards.SingleOrDefaultAsync(x=>x.CustomerId==customer,ct);
        if(guard==null||guard.OperationId!=input.OperationId||guard.State=="Released"||await Outstanding(customer,ct))throw new BookingRequestException(409,"Account cleanup is not eligible.");
        // Rental stores customer/actor IDs, not customer contact/document copies. Preserve financial and handover history.
        guard.State="Purged";await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return new{success=true};
    }
}
