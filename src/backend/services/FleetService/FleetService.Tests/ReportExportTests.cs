using System.Globalization;
using System.Net;
using System.Text;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace FleetService.Tests;

public sealed class ReportExportTests
{
    [Theory]
    [InlineData("bookings/export", null, 401)]
    [InlineData("bookings/export", "CUSTOMER", 403)]
    [InlineData("bookings/export", "FLEET_MANAGER", 403)]
    [InlineData("bookings/export", "MAINTENANCE_STAFF", 403)]
    [InlineData("bookings/export", "ADMIN", 200)]
    [InlineData("maintenance/export", null, 401)]
    [InlineData("maintenance/export", "CUSTOMER", 403)]
    [InlineData("maintenance/export", "ADMIN", 200)]
    [InlineData("maintenance/export", "FLEET_MANAGER", 200)]
    [InlineData("maintenance/export", "MAINTENANCE_STAFF", 200)]
    public async Task ExportPreservesReportPermissionsAndNeverCaches(string path, string? role, int expected)
    {
        using var app = new CustomWebApplicationFactory<ReportsController>();
        using var client = ReportsControllerTests.Client(app, role);
        var response = await client.GetAsync("/api/reports/" + path);
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        if (expected == 200) {
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.StartsWith("fleetflow-", response.Content.Headers.ContentDisposition?.FileNameStar);
            Assert.Equal(new byte[] { 239, 187, 191 }, (await response.Content.ReadAsByteArrayAsync())[..3]);
        }
    }

    [Fact]
    public async Task BookingExportContainsAllFilteredRowsBeyondFirstPageAndEscapesVehicleText()
    {
        using var app = new CustomWebApplicationFactory<ReportsController>();
        using var client = ReportsControllerTests.Client(app, "ADMIN");
        var wanted = new List<Guid>(); var excluded = Guid.NewGuid();
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            var vehicle = new Vehicle { Id = Guid.NewGuid(), LicensePlate = "=HYPERLINK(\"bad\")", Make = "Test, make", Model = "Car\nmodel" };
            db.Vehicles.Add(vehicle);
            for (var i = 0; i < 28; i++) {
                var id = i == 27 ? excluded : Guid.NewGuid(); if (i < 27) wanted.Add(id);
                db.Bookings.Add(new() { Id = id, CustomerId = Guid.NewGuid(), VehicleId = vehicle.Id,
                    Status = i == 27 ? BookingStatus.Confirmed : BookingStatus.Cancelled,
                    StartDateTime = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc), EndDateTime = new(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc), TotalCost = 123.45m });
            }
            await db.SaveChangesAsync();
        }
        var result = await client.GetStringAsync("/api/reports/bookings/export?status=Cancelled");
        Assert.All(wanted, id => Assert.Contains(id.ToString(), result)); Assert.DoesNotContain(excluded.ToString(), result);
        Assert.Contains("\"\t=HYPERLINK(\"\"bad\"\")\"", result);
        Assert.Contains("\"Test, make\"", result); Assert.Contains("\"Car\nmodel\"", result);
        Assert.Contains("\"123.45\"", result); Assert.Contains("2026-10-14T08:00:00.0000000Z", result);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/bookings/export?status=unknown")).StatusCode);
    }

    [Theory]
    [InlineData("=SUM(1,2)")][InlineData(" +1")][InlineData("-1+2")][InlineData("@SUM(1)")]
    [InlineData("\t=1")][InlineData("\r=1")][InlineData("\n=1")][InlineData("＝1+2")]
    public void FormulaLookingTextIsQuotedAndPrefixedWithoutChangingNumericCells(string text)
    {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = Encoding.UTF8.GetString(ReportCsv.Encode(["Text", "Amount"], [new object?[] { text, 123.45m }]));
            Assert.Contains("\"\t" + text.Replace("\"", "\"\"") + "\"", result);
            Assert.Contains("\"123.45\"", result); Assert.DoesNotContain("\"123,45\"", result);
        } finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void MaintenanceExportSeparatesCurrentStateFromHistoryAndPreservesUnknownCosts()
    {
        var vehicle = Guid.NewGuid(); var record = Guid.NewGuid();
        var report = new MaintenanceReportResponse(DateTime.UtcNow,
            [new(vehicle, "QA-1", "Toyota", "Corolla", "Colombo", 1000, VehicleStatus.Maintenance, null)],
            new(true, null, [new(record, vehicle, "QA-1", "Inspect, brakes", "COMPLETED", null, null, null, [])]));
        var csv = Encoding.UTF8.GetString(ReportCsv.Maintenance(report));
        Assert.Contains("\"Current vehicle\"", csv); Assert.Contains("\"Maintenance activity\"", csv);
        Assert.Contains(record.ToString(), csv); Assert.Contains("\"Inspect, brakes\"", csv);
        Assert.Contains("\"\",\"\",\"[]\"", csv); // Unrecorded cost/update remain empty, not zero or invented history.
        Assert.Throws<TimeoutException>(() => ReportCsv.Maintenance(report with { WorkOrders = new(false, "private failure", []) }));
    }

    [Fact]
    public async Task OversizedBookingExportIsRejectedRatherThanTruncated()
    {
        await using var db = new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vehicle = new Vehicle { Id = Guid.NewGuid(), LicensePlate = "QA-LIMIT", Make = "Toyota", Model = "Corolla" };
        db.Vehicles.Add(vehicle);
        db.Bookings.AddRange(Enumerable.Range(0, BookingExportQuery.MaxRows + 1).Select(_ => new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, Status = BookingStatus.Confirmed }));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new ReportingService(db, new UnavailableMaintenanceReportSource(), TimeProvider.System).GetBookingExportAsync(new()));
    }

    private sealed class BrokenSource : IMaintenanceReportSource {
        public Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default) => Task.FromResult(new MaintenanceRecordsSnapshot(false, "private failure details", []));
    }
    [Fact]
    public async Task UnavailableMaintenanceSourceReturns503WithoutPartialCsvOrPrivateDetails()
    {
        using var source = new CustomWebApplicationFactory<ReportsController>();
        using var app = source.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IMaintenanceReportSource>(); s.AddSingleton<IMaintenanceReportSource, BrokenSource>(); }));
        using var client = ReportsControllerTests.Client(app, "ADMIN");
        var response = await client.GetAsync("/api/reports/maintenance/export"); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("private failure details", body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
