using FleetFlow.Contracts;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using RentalMigration;
using RentalService.Api.Data;
using RentalService.Api.Services;
namespace RentalService.Tests;

public sealed class ReturnTests
{
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private static ReturnRequest Request(decimal charge = 0) => new() { Odometer = 1500, FuelPercentage = 40,
        Condition = "Exterior and cabin inspected on return.", ExtraCharges = charge, ExtraChargeReason = charge > 0 ? "Additional cleaning agreed at return." : null };
    private static async Task<RentalResponse> Start(RentalPostgresFixture f)
    {
        await using var db = f.Rental(); var gateway = new BookingRecoveryTests.Gateway(f);
        var booking = await new BookingCoordinator(db, gateway).CreateAsync(f.Customer, BookingRecoveryTests.Request(f), Guid.NewGuid());
        return (await new PickupCoordinator(db, gateway, new Clock(booking.StartDateTime)).StartAsync(booking.Id, Guid.NewGuid(), new() { Odometer = 1000, FuelPercentage = 80, Condition = "Bodywork checked." }))!;
    }
    private sealed class LostCompletion(RentalPostgresFixture f) : IFleetGateway
    {
        private readonly BookingRecoveryTests.Gateway inner = new(f); private bool lost;
        public Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand c, CancellationToken ct) => inner.ReserveAsync(c, ct);
        public Task<ReservationAcknowledgement> ReleaseAsync(Guid id, CancellationToken ct) => inner.ReleaseAsync(id, ct);
        public Task<FleetService.Api.Dtos.VehicleResponse> VehicleAsync(Guid id, CancellationToken ct) => inner.VehicleAsync(id, ct);
        public Task<bool> AvailableAsync(Guid id, DateTime a, DateTime b, CancellationToken ct) => inner.AvailableAsync(id, a, b, ct);
        public Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand c, CancellationToken ct) => inner.HoldAsync(c, ct);
        public async Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand c, CancellationToken ct) {
            var ack = await inner.CompleteAsync(c, ct); if (!lost) { lost = true; throw new BookingRequestException(503, "Lost committed return response."); } return ack;
        }
    }

    [RentalPostgresFact] public async Task ReturnCompletesBookingKeepsEstimateAndUpdatesVehicleAndTimeline()
    {
        await using var f = await RentalPostgresFixture.Create(); var started = await Start(f); var actor = Guid.NewGuid();
        await using (var db = f.Rental()) {
            var service = new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddHours(1)));
            var result = (await service.CompleteAsync(started.Id, actor, Request(500)))!;
            Assert.Equal(RentalLifecycleState.Completed, result.State); Assert.Equal(BookingStatus.Completed, result.Booking.Status);
            Assert.Equal(started.Booking.TotalCost, result.Booking.TotalCost); Assert.Equal(started.Booking.TotalCost + 500, result.Return!.FinalAmount);
            Assert.Equal(result.Return.FinalAmount, result.Booking.FinalRentalAmount); Assert.Equal(actor, result.Return.CompletedByUserId); Assert.False(result.Return.IsLate);
            Assert.Equal(409, (await Assert.ThrowsAsync<BookingRequestException>(() => service.CompleteAsync(started.Id, actor, Request(500)))).Status);
            Assert.Single(await db.NotificationOutbox.Where(e => e.EventType == "RentalCompletedEvent").ToListAsync());
            var timeline = (await new RentalTimelineService(db).GetAsync(started.BookingId, f.Customer))!;
            Assert.Equal(new[] { "Created", "Confirmed", "Pickup", "Return", "Completed" }, timeline.Items.Select(e => e.Kind));
            Assert.Null(await new RentalTimelineService(db).GetAsync(started.BookingId, Guid.NewGuid()));
            Assert.Single((await new PickupCoordinator(db, new BookingRecoveryTests.Gateway(f), TimeProvider.System).QueueAsync("Completed", 1, 25)).Items);
        }
        await using var fleet = f.Fleet(); var ledger = await fleet.VehicleReservations.SingleAsync();
        Assert.True(ledger.Released); Assert.False(ledger.IsHeld); Assert.Equal(1500, ledger.ReturnOdometer);
        var vehicle = await fleet.Vehicles.SingleAsync(); Assert.Equal(1500, vehicle.Mileage); Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [RentalPostgresFact] public async Task LatenessHasNoInventedFeeAndReturnAtBookedEndIsOnTime()
    {
        foreach (var late in new[] { false, true }) {
            await using var f = await RentalPostgresFixture.Create(); var started = await Start(f); await using var db = f.Rental();
            var now = late ? started.Booking.EndDateTime.AddMinutes(1) : started.Booking.EndDateTime;
            var result = (await new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(now)).CompleteAsync(started.Id, f.Customer, Request()))!;
            Assert.Equal(late, result.Return!.IsLate); Assert.Equal(0, result.Return.ExtraCharges); Assert.Equal(started.Booking.TotalCost, result.Return.FinalAmount);
        }
    }

    [RentalPostgresFact] public async Task InvalidMileageTimeAndChargeReasonDoNotCreateReturnIntent()
    {
        await using var f = await RentalPostgresFixture.Create(); var started = await Start(f); await using var db = f.Rental();
        var service = new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddHours(1)));
        foreach (var request in new[] { new ReturnRequest { Odometer = 999, FuelPercentage = 30, Condition = "Checked" },
            new() { Odometer = 1500, FuelPercentage = 101, Condition = "Checked" }, new() { Odometer = 1500, FuelPercentage = 30, Condition = "Checked", ExtraCharges = 1 },
            new() { Odometer = 1500, FuelPercentage = 30, Condition = "Checked", ExtraCharges = 1.001m, ExtraChargeReason = "Cleaning" } })
            Assert.Equal(400, (await Assert.ThrowsAsync<BookingRequestException>(() => service.CompleteAsync(started.Id, f.Customer, request))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<BookingRequestException>(() => new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddSeconds(-1))).CompleteAsync(started.Id, f.Customer, Request()))).Status);
        Assert.Empty(await db.Returns.ToListAsync()); Assert.Equal(RentalLifecycleState.Active, (await db.Pickups.SingleAsync()).State);
    }

    [RentalPostgresFact] public async Task LostReturnResponseRecoversOriginalDetailsAndDoesNotOverwriteLaterHandover()
    {
        await using var f = await RentalPostgresFixture.Create(); var started = await Start(f); var at = started.PickupAt.AddHours(1); var actor = Guid.NewGuid();
        await using (var db = f.Rental()) {
            var service = new ReturnCoordinator(db, new LostCompletion(f), new Clock(at));
            Assert.Equal(503, (await Assert.ThrowsAsync<BookingRequestException>(() => service.CompleteAsync(started.Id, actor, Request(500)))).Status);
            Assert.Equal(RentalLifecycleState.CompletionPending, (await db.Pickups.SingleAsync()).State);
            Assert.Equal(409, (await Assert.ThrowsAsync<BookingRequestException>(() => service.CompleteAsync(started.Id, actor, Request(600)))).Status);
        }
        Guid second;
        await using (var db = f.Rental()) {
            var gateway = new BookingRecoveryTests.Gateway(f);
            var request = new FleetService.Api.Dtos.CreateBookingRequest { VehicleId = f.VehicleId, StartDateTime = at.AddHours(1), EndDateTime = at.AddHours(3) };
            var booking = await new BookingCoordinator(db, gateway).CreateAsync(f.Customer, request, Guid.NewGuid()); second = booking.Id;
            await new PickupCoordinator(db, gateway, new Clock(request.StartDateTime)).StartAsync(second, actor, new() { Odometer = 2000, FuelPercentage = 80, Condition = "Checked again." });
        }
        await using (var db = f.Rental()) {
            var returns = new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(at.AddDays(1)));
            await new BookingCoordinator(db, new BookingRecoveryTests.Gateway(f), returns: returns).ResumeAsync(started.BookingId);
            await returns.ResumeAsync(started.BookingId);
            var saved = await db.Returns.SingleAsync(); Assert.Equal(at, saved.ReturnedAt); Assert.Equal(actor, saved.CompletedByUserId); Assert.Equal(500, saved.ExtraCharges);
            Assert.Single(await db.NotificationOutbox.Where(e => e.EventType == "RentalCompletedEvent").ToListAsync());
            Assert.Equal(RentalLifecycleState.Active, (await db.Pickups.SingleAsync(p => p.BookingId == second)).State);
        }
        await using var fleet = f.Fleet(); var vehicle = await fleet.Vehicles.SingleAsync();
        Assert.Equal(VehicleStatus.InUse, vehicle.Status); Assert.Equal(2000, vehicle.Mileage);
    }

    [RentalPostgresFact] public async Task EventCommitFailureKeepsPendingReturnAndRecoveryWritesHistoryOnce()
    {
        await using var f = await RentalPostgresFixture.Create(); var started = await Start(f);
        await using (var db = f.Rental()) {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" ADD CONSTRAINT reject_completion CHECK("EventType" <> 'RentalCompletedEvent')""");
            await Assert.ThrowsAsync<DbUpdateException>(() => new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddHours(1))).CompleteAsync(started.Id, f.Customer, Request()));
        }
        await using (var db = f.Rental()) {
            Assert.Equal(RentalLifecycleState.CompletionPending, (await db.Pickups.SingleAsync()).State); Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.SingleAsync()).Status);
            Assert.Empty(await db.LifecycleEvents.Where(e => e.Kind == "Completed").ToListAsync());
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "NotificationOutbox" DROP CONSTRAINT reject_completion""");
            await new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddHours(2))).ResumeAsync(started.BookingId);
            Assert.Single(await db.LifecycleEvents.Where(e => e.Kind == "Completed").ToListAsync());
            Assert.Single(await db.NotificationOutbox.Where(e => e.EventType == "RentalCompletedEvent").ToListAsync());
        }
    }

    [RentalPostgresFact] public async Task ConcurrentReturnsHaveOneFinalAmountAndOneCompletionEvent()
    {
        await using var f = await RentalPostgresFixture.Create(); var started = await Start(f);
        async Task<int> Complete() { await using var db = f.Rental(); try { await new ReturnCoordinator(db, new BookingRecoveryTests.Gateway(f), new Clock(started.PickupAt.AddHours(1))).CompleteAsync(started.Id, Guid.NewGuid(), Request(500)); return 200; } catch (BookingRequestException ex) { return ex.Status; } }
        var results = await Task.WhenAll(Complete(), Complete()); Assert.Contains(200, results); Assert.All(results, n => Assert.Contains(n, new[] { 200, 409 }));
        await using var check = f.Rental(); Assert.Single(await check.Returns.ToListAsync()); Assert.Single(await check.NotificationOutbox.Where(e => e.EventType == "RentalCompletedEvent").ToListAsync());
    }

    [RentalPostgresFact] public async Task SchemaUpgradeIsIdempotentAndHistoricalTimelineNeverInventsHandover()
    {
        await using var f = await RentalPostgresFixture.Create(); await ReturnSchemaUpgrade.ApplyAsync(f.Source, f.Target); await ReturnSchemaUpgrade.ApplyAsync(f.Source, f.Target);
        await using var db = f.Rental(); var id = Guid.NewGuid(); db.Bookings.Add(new() { Id = id, CustomerId = f.Customer, VehicleId = f.VehicleId,
            Status = BookingStatus.Completed, CreatedAt = DateTime.UtcNow.AddDays(-3), UpdatedAt = null }); await db.SaveChangesAsync();
        var timeline = (await new RentalTimelineService(db).GetAsync(id, f.Customer))!;
        Assert.Null(timeline.RentalId); Assert.Null(timeline.Items.Single(e => e.Kind == "Confirmed").OccurredAt);
        Assert.DoesNotContain(timeline.Items, e => e.Kind is "Pickup" or "Return"); Assert.Null(timeline.Items.Last().OccurredAt);
    }
}
