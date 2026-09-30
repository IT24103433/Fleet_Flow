using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FleetService.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))
            Skip = "Set FLEET_TEST_POSTGRES to an isolated fleetflow_hardening_* PostgreSQL database.";
    }
}

// Never uses the application's connection string, migrations, or developer database.
public sealed class PostgresVehicleConcurrencyTests : IAsyncLifetime
{
    private string connectionString = "";
    private readonly string applicationName = $"hardening-{Guid.NewGuid():N}";
    private FleetDbContext Context() => new(new DbContextOptionsBuilder<FleetDbContext>().UseNpgsql(connectionString).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var settings = new NpgsqlConnectionStringBuilder(configured);
        if (settings.Host is not ("localhost" or "127.0.0.1") ||
            settings.Database?.StartsWith("fleetflow_hardening_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Concurrency tests require a local isolated fleetflow_hardening_* database.");
        settings.ApplicationName = applicationName;
        connectionString = settings.ConnectionString;
        await using var db = Context();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedAsync()
    {
        await using var db = Context();
        var category = new VehicleCategory { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString("N") };
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(), Category = category, VehicleCategoryId = category.Id,
            Vin = Guid.NewGuid().ToString("N")[..17], LicensePlate = Guid.NewGuid().ToString("N")[..16],
            Make = "Test", Model = "Test", DailyRate = 100, Status = VehicleStatus.Available,
            Transmission = "Automatic", FuelType = "Petrol", SeatingCapacity = "5", HubLocation = "Test"
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    private static CreateBookingRequest Booking(Guid vehicleId, int startDay = 1) => new()
    {
        VehicleId = vehicleId, StartDateTime = DateTime.UtcNow.Date.AddDays(startDay), EndDateTime = DateTime.UtcNow.Date.AddDays(startDay + 1)
    };
    private static CreateMaintenanceRecordRequest Maintenance(Guid vehicleId) => new()
    {
        VehicleId = vehicleId, ScheduledDateTime = DateTime.UtcNow.AddDays(10), ServiceInformation = "Test service", Cost = 0
    };

    private async Task<Exception?[]> Race(Guid vehicleId, params Func<FleetDbContext, Task>[] operations)
    {
        await using var gate = new NpgsqlConnection(connectionString);
        await gate.OpenAsync();
        await using var transaction = await gate.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT \"Id\" FROM \"Vehicles\" WHERE \"Id\" = @id FOR UPDATE", gate, transaction))
        {
            command.Parameters.AddWithValue("id", vehicleId);
            await command.ExecuteScalarAsync();
        }

        var pending = operations.Select(async operation =>
        {
            await using var db = Context();
            return await Record.ExceptionAsync(() => operation(db));
        }).ToArray();

        try
        {
            await using var observer = new NpgsqlConnection(connectionString);
            await observer.OpenAsync();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            var waiting = 0L;
            while (DateTime.UtcNow < deadline)
            {
                await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock'", observer);
                command.Parameters.AddWithValue("app", applicationName);
                waiting = (long)(await command.ExecuteScalarAsync())!;
                if (waiting >= operations.Length) break;
                await Task.Delay(25);
            }
            Assert.True(waiting >= operations.Length, "Every competing write must wait on the PostgreSQL vehicle lock before validation.");
        }
        finally
        {
            await transaction.RollbackAsync();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(30));
        }
        return await Task.WhenAll(pending);
    }

    [PostgresFact]
    public async Task OverlappingBookings_OnlyOneCommits()
    {
        var id = await SeedAsync();
        var errors = await Race(id,
            db => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), Booking(id)),
            db => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), Booking(id)));
        Assert.Single(errors, e => e == null);
        Assert.IsType<DuplicateException>(Assert.Single(errors, e => e != null));
        await using var verify = Context();
        Assert.Equal(1, await verify.Bookings.CountAsync(b => b.VehicleId == id));
    }

    [PostgresFact]
    public async Task AdjacentBookings_BothCommit()
    {
        var id = await SeedAsync();
        var errors = await Race(id,
            db => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), Booking(id, 1)),
            db => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), Booking(id, 2)));
        Assert.All(errors, e => Assert.Null(e));
    }

    [PostgresFact]
    public async Task DuplicateScheduling_OnlyOneActiveRecord()
    {
        var id = await SeedAsync();
        var errors = await Race(id,
            db => new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), Maintenance(id)),
            db => new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), Maintenance(id)));
        Assert.Single(errors, e => e == null);
        Assert.IsType<DuplicateException>(Assert.Single(errors, e => e != null));
        await using var verify = Context();
        Assert.Equal(1, await verify.MaintenanceRecords.CountAsync(r => r.VehicleId == id));
        Assert.Equal(VehicleStatus.Maintenance, (await verify.Vehicles.FindAsync(id))!.Status);
    }

    [PostgresFact]
    public async Task BookingAndScheduling_CannotBothCommit()
    {
        var id = await SeedAsync();
        var errors = await Race(id,
            db => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), Booking(id)),
            db => new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), Maintenance(id)));
        Assert.Single(errors, e => e == null);
        Assert.Single(errors, e => e is DuplicateException or ValidationException);
        await using var verify = Context();
        Assert.Equal(1, await verify.Bookings.CountAsync(b => b.VehicleId == id) + await verify.MaintenanceRecords.CountAsync(r => r.VehicleId == id));
    }

    [PostgresFact]
    public async Task CompetingStatusChanges_CannotLeaveActiveRecordWithAvailableVehicle()
    {
        var id = await SeedAsync();
        Guid recordId;
        await using (var setup = Context())
            recordId = (await new MaintenanceService(setup).CreateMaintenanceRecordAsync(Guid.NewGuid(), Maintenance(id))).Id;
        var errors = await Race(id,
            db => new MaintenanceService(db).UpdateMaintenanceStatusAsync(recordId, MaintenanceStatus.IN_PROGRESS),
            db => new MaintenanceService(db).UpdateMaintenanceStatusAsync(recordId, MaintenanceStatus.CANCELLED));
        Assert.All(errors, e => Assert.True(e == null || e is ValidationException));
        await using var verify = Context();
        Assert.Equal(MaintenanceStatus.CANCELLED, (await verify.MaintenanceRecords.FindAsync(recordId))!.Status);
        Assert.Equal(VehicleStatus.Available, (await verify.Vehicles.FindAsync(id))!.Status);
    }

    [PostgresFact]
    public async Task ManualAvailabilityAndScheduling_PreserveMaintenanceBlock()
    {
        var id = await SeedAsync();
        var errors = await Race(id,
            db => new VehicleService(db).UpdateVehicleStatusAsync(id, new UpdateVehicleStatusRequest { Status = VehicleStatus.Available }),
            db => new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), Maintenance(id)));
        Assert.Null(errors[1]);
        Assert.True(errors[0] == null || errors[0] is ValidationException);
        await using var verify = Context();
        Assert.Equal(VehicleStatus.Maintenance, (await verify.Vehicles.FindAsync(id))!.Status);
    }

    [PostgresFact]
    public async Task DuplicateCancellation_OnlyOneSucceeds_AndPeriodCanBeRebooked()
    {
        var id = await SeedAsync();
        var customer = Guid.NewGuid();
        Guid bookingId;
        await using (var setup = Context())
            bookingId = (await new BookingService(setup).CreateBookingAsync(customer, Booking(id))).Id;
        var errors = await Race(id,
            db => new BookingService(db).CancelBookingAsync(bookingId, customer),
            db => new BookingService(db).CancelBookingAsync(bookingId, customer));
        Assert.Single(errors, e => e == null);
        Assert.IsType<DuplicateException>(Assert.Single(errors, e => e != null));
        await using var verify = Context();
        await new BookingService(verify).CreateBookingAsync(Guid.NewGuid(), Booking(id));
        Assert.Equal(2, await verify.Bookings.CountAsync(b => b.VehicleId == id));
    }
}
