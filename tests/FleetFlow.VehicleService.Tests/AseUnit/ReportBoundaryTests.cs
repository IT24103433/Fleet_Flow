using FleetService.Api.Controllers;
using FleetService.Api.Dtos;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace FleetFlow.VehicleService.Tests.AseUnit;

[Trait("Category", "ASE.Unit")]
[Trait("Owner", "Sanjeevsaran")]
public sealed class ReportBoundaryTests
{
    private static ReportsController Controller(IReportingService service) => new(service, NullLogger<ReportsController>.Instance)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

    public static TheoryData<Exception, int> Failures => new()
    {
        { new ArgumentException("Synthetic bad filter"), 400 },
        { new TimeoutException("Synthetic timeout"), 503 },
        { new NpgsqlException("Synthetic database outage"), 503 }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task BookingReport_MapsExpectedDependencyFailuresAndForwardsCancellation(Exception failure, int status)
    {
        var reports = new Mock<IReportingService>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        var query = new BookingReportQuery { Page = 2, PageSize = 10 };
        reports.Setup(r => r.GetBookingReportAsync(query, cancellation.Token)).ThrowsAsync(failure);

        var result = Assert.IsAssignableFrom<ObjectResult>((await Controller(reports.Object).GetBookings(query, cancellation.Token)).Result);

        Assert.Equal(status, result.StatusCode);
        if (status == 503)
        {
            var details = Assert.IsType<ProblemDetails>(result.Value);
            Assert.Equal(503, details.Status);
            Assert.Equal("Reporting data is unavailable. Please try again.", details.Title);
            Assert.Null(details.Detail);
        }
        reports.Verify(r => r.GetBookingReportAsync(query, cancellation.Token), Times.Once);
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BookingReport_ReturnsExactSnapshotIncludingUnavailableMetrics()
    {
        var reports = new Mock<IReportingService>(MockBehavior.Strict);
        var snapshot = new FleetSummaryResponse(DateTime.UnixEpoch, new(0, 0, 0, 0, 0), 0, 0, null);
        reports.Setup(r => r.GetFleetSummaryAsync(CancellationToken.None)).ReturnsAsync(snapshot);
        var result = Assert.IsType<OkObjectResult>((await Controller(reports.Object).GetFleetSummary(CancellationToken.None)).Result);
        Assert.Same(snapshot, result.Value);
        Assert.Null(((FleetSummaryResponse)result.Value!).CurrentBookingUtilizationPercent);
        reports.Verify(r => r.GetFleetSummaryAsync(CancellationToken.None), Times.Once);
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BookingExport_ProducesNamedCsvFromTheReturnedSnapshot()
    {
        var reports = new Mock<IReportingService>(MockBehavior.Strict);
        var query = new BookingExportQuery();
        reports.Setup(r => r.GetBookingExportAsync(query, CancellationToken.None)).ReturnsAsync(new BookingExportResponse(DateTime.UnixEpoch, []));
        var result = Assert.IsType<FileContentResult>(await Controller(reports.Object).ExportBookings(query, CancellationToken.None));
        Assert.Equal("fleetflow-bookings.csv", result.FileDownloadName);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, result.FileContents[..3]);
        Assert.Contains("Reserved amount (LKR)", System.Text.Encoding.UTF8.GetString(result.FileContents));
        reports.Verify(r => r.GetBookingExportAsync(query, CancellationToken.None), Times.Once);
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MaintenanceExport_UnavailableSnapshot_Is503RatherThanCsv()
    {
        var reports = new Mock<IReportingService>(MockBehavior.Strict);
        reports.Setup(r => r.GetMaintenanceReportAsync(CancellationToken.None)).ReturnsAsync(new MaintenanceReportResponse(DateTime.UnixEpoch, [], new(false, "Unavailable", [])));
        var result = Assert.IsType<ObjectResult>(await Controller(reports.Object).ExportMaintenance(CancellationToken.None));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("The report could not be exported. Please try again.", Assert.IsType<ProblemDetails>(result.Value).Title);
        reports.Verify(r => r.GetMaintenanceReportAsync(CancellationToken.None), Times.Once);
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CallerCancellation_IsNotRelabelledAsSuccessfulOrUnavailableReport()
    {
        var reports = new Mock<IReportingService>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        reports.Setup(r => r.GetFleetSummaryAsync(cancellation.Token)).ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(reports.Object).GetFleetSummary(cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        reports.Verify(r => r.GetFleetSummaryAsync(cancellation.Token), Times.Once);
        reports.VerifyNoOtherCalls();
    }
}
