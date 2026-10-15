using FleetFlow.Contracts;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RentalService.Api.Data;
using RentalService.Api.Services;

namespace RentalService.Tests;
public sealed class AccountLifecycleTests
{
    private static RentalDbContext Database()=>new(new DbContextOptionsBuilder<RentalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AccountLifecycleService Service(RentalDbContext db)=>new(db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["REQUIRE_DRIVER_VERIFICATION"]="true"}).Build());
    [Fact]public async Task FutureConfirmedBookingsAndUncertainWorkflowsBlockDeletion()
    {
        await using var db=Database();var customer=Guid.NewGuid();var service=Service(db);
        db.Bookings.Add(new(){Id=Guid.NewGuid(),CustomerId=customer,Status=BookingStatus.Confirmed,StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(2)});await db.SaveChangesAsync();
        Assert.True(await service.Outstanding(customer,default));await Assert.ThrowsAsync<BookingRequestException>(()=>service.Freeze(customer,new(Guid.NewGuid()),default));Assert.Empty(db.AccountGuards);
        db.Bookings.Single().Status=BookingStatus.Cancelled;await db.SaveChangesAsync();Assert.False(await service.Outstanding(customer,default));
        db.Workflows.Add(new(){Id=Guid.NewGuid(),CustomerId=customer,Phase="ReserveRequested"});await db.SaveChangesAsync();Assert.True(await service.Outstanding(customer,default));
    }
    [Fact]public async Task FreezeIsIdempotentRestorationUsesMatchingOperationAndPurgeRetainsHistory()
    {
        await using var db=Database();var customer=Guid.NewGuid();var operation=Guid.NewGuid();var service=Service(db);
        var history=new RentalBooking{Id=Guid.NewGuid(),CustomerId=customer,Status=BookingStatus.Completed,TotalCost=9000,EndDateTime=DateTime.UtcNow.AddDays(-1)};db.Bookings.Add(history);await db.SaveChangesAsync();
        await service.Freeze(customer,new(operation),default);await service.Freeze(customer,new(operation),default);Assert.Single(db.AccountGuards);
        await Assert.ThrowsAsync<BookingRequestException>(()=>service.Release(customer,new(Guid.NewGuid()),default));
        await service.Release(customer,new(operation),default);Assert.Equal("Released",db.AccountGuards.Single().State);
        var next=Guid.NewGuid();await service.Freeze(customer,new(next),default);await service.Anonymize(customer,new(next),default);
        Assert.Equal("Purged",db.AccountGuards.Single().State);Assert.Equal(9000,db.Bookings.Single().TotalCost);
        await Assert.ThrowsAsync<BookingRequestException>(()=>service.Release(customer,new(next),default));
    }
}
