using System.Net;
using System.Net.Http.Json;
using System.Text;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Moq;
using Moq.Protected;

namespace FleetFlow.VehicleService.Tests.AseMutation;

// Added after inspecting the genuine Stryker baseline, without changing production logic.
[Trait("Category", "ASE.Mutation")]
[Trait("Owner", "Jadavan")]
public sealed class MutationRegressionTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task ErrorStatus_WithOtherwiseValidStaffJson_IsStillRejected(int status)
    {
        var id = Guid.Parse("12345678-1234-4234-8234-123456789abc");
        var account = new MaintenanceStaffAccount(id, "Synthetic worker", "worker_mutation");
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage((HttpStatusCode)status) { Content = JsonContent.Create(account) });
        var tokens = new Mock<IServiceTokenProvider>(MockBehavior.Strict);
        tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("synthetic-service-token");
        using var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://identity.mutation.invalid/") };
        var subject = new MaintenanceStaffDirectory(http, tokens.Object);

        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => subject.GetAsync(id, CancellationToken.None));

        handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        tokens.Verify(t => t.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        tokens.VerifyNoOtherCalls();
        handler.VerifyNoOtherCalls();
    }

    [Fact]
    public void BookingExport_EmptyResultStillHasTheCompleteOrderedSchema()
    {
        var result = ReportCsv.Bookings(new BookingExportResponse(DateTime.UnixEpoch, []));
        Assert.Equal("\uFEFF\"Booking ID\",\"Customer ID\",\"Vehicle ID\",\"License plate\",\"Make\",\"Model\",\"Status\",\"Starts (UTC)\",\"Ends (UTC)\",\"Reserved amount (LKR)\",\"Created (UTC)\",\"Updated (UTC)\"\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void MaintenanceExport_MixedSourcesPreserveTheSchemaAndBothKindsOfRows()
    {
        var vehicleId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var workId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var vehicle = new MaintenanceVehicleRow(vehicleId, "QA-CSV", "Toyota", "Corolla", "Colombo", 123,
            VehicleStatus.Maintenance, DateTime.UnixEpoch);
        var activity = new MaintenanceRecordRow(workId, vehicleId, "QA-CSV", "Oil change", "Completed",
            DateTime.UnixEpoch, DateTime.UnixEpoch.AddHours(1), 2500.75m, []);
        var report = new MaintenanceReportResponse(DateTime.UnixEpoch, [vehicle], new(true, null, [activity]));

        var actual = Encoding.UTF8.GetString(ReportCsv.Maintenance(report));

        var expected = "\uFEFF\"Record type\",\"Record ID\",\"Vehicle ID\",\"License plate\",\"Make\",\"Model\",\"Hub\",\"Mileage (km)\",\"Status\",\"Activity\",\"Scheduled (UTC)\",\"Completed (UTC)\",\"Recorded cost (LKR)\",\"Last vehicle update (UTC)\",\"History (JSON)\"\r\n"
            + "\"Current vehicle\",\"11111111-1111-4111-8111-111111111111\",\"11111111-1111-4111-8111-111111111111\",\"QA-CSV\",\"Toyota\",\"Corolla\",\"Colombo\",\"123\",\"Maintenance\",\"\",\"\",\"\",\"\",\"1970-01-01T00:00:00.0000000Z\",\"\"\r\n"
            + "\"Maintenance activity\",\"22222222-2222-4222-8222-222222222222\",\"11111111-1111-4111-8111-111111111111\",\"QA-CSV\",\"\",\"\",\"\",\"\",\"Completed\",\"Oil change\",\"1970-01-01T00:00:00.0000000Z\",\"1970-01-01T01:00:00.0000000Z\",\"2500.75\",\"\",\"[]\"\r\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Encode_LocalTimestampIsConvertedToUtcRatherThanRelabelled()
    {
        var local = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(local).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var actual = Encoding.UTF8.GetString(ReportCsv.Encode(["Time"], [new object?[] { local }]));
        Assert.Equal("\uFEFF\"Time\"\r\n\"" + expectedUtc + "\"\r\n", actual);
    }
}
