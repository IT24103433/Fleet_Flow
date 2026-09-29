using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Tests;

public class BookingNotificationDispatchTests
{
    private sealed class Dispatcher(FleetDbContext db, bool fail, bool full) : INotificationEventDispatcher
    {
        public BookingCreatedEvent? Captured { get; private set; }
        public bool TryEnqueue(NotificationDomainEvent domainEvent)
        {
            Captured = Assert.IsType<BookingCreatedEvent>(domainEvent);
            Assert.True(db.Bookings.AsNoTracking().Any(b => b.Id == Captured.BookingId));
            if (fail) throw new InvalidOperationException("Dispatcher unavailable");
            return !full;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BookingRemainsPersisted_WhenDispatchThrowsOrQueueIsFull(bool fail, bool full)
    {
        using var db = new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var category = new VehicleCategory { Id = Guid.NewGuid(), Name = "Test" };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), VehicleCategoryId = category.Id, DailyRate = 100 };
        db.VehicleCategories.Add(category);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var dispatcher = new Dispatcher(db, fail, full);
        var customer = Guid.NewGuid();
        var result = await new BookingService(db, dispatcher).CreateBookingAsync(customer, new CreateBookingRequest
        { VehicleId = vehicle.Id, StartDateTime = DateTime.UtcNow.AddDays(1), EndDateTime = DateTime.UtcNow.AddDays(2) });
        Assert.Equal(result.Id, Assert.Single(db.Bookings).Id);
        Assert.Equal(result.Id, dispatcher.Captured!.BookingId);
        Assert.Equal(customer, dispatcher.Captured.CustomerId);
        Assert.Equal(vehicle.Id, dispatcher.Captured.VehicleId);
        Assert.NotEqual(Guid.Empty, dispatcher.Captured.EventId);
        Assert.Equal(DateTimeKind.Utc, dispatcher.Captured.OccurredAt.Kind);
    }
}
