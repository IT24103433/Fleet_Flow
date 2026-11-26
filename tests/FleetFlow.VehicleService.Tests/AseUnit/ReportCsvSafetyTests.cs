using System.Globalization;
using System.Text;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Moq;

namespace FleetFlow.VehicleService.Tests.AseUnit;

[Trait("Category", "ASE.Unit")]
[Trait("Owner", "Sanjeevsaran")]
public sealed class ReportCsvSafetyTests
{
    [Theory]
    [InlineData("=1+1", "\t=1+1")]
    [InlineData("+SUM(A1)", "\t+SUM(A1)")]
    [InlineData("-1+1", "\t-1+1")]
    [InlineData("@cmd", "\t@cmd")]
    [InlineData("  =1", "\t  =1")]
    [InlineData("\u0001=1", "\t\u0001=1")]
    [InlineData("＝1", "\t＝1")]
    [InlineData("＋1", "\t＋1")]
    [InlineData("－1", "\t－1")]
    [InlineData("＠cmd", "\t＠cmd")]
    [InlineData("\tvalue", "\t\tvalue")]
    [InlineData("\rvalue", "\t\rvalue")]
    [InlineData("\nvalue", "\t\nvalue")]
    [InlineData("Toyota", "Toyota")]
    [InlineData("", "")]
    [InlineData("123", "123")]
    [InlineData("A=B", "A=B")]
    public void Encode_UntrustedStrings_AreQuotedAndFormulaTriggersAreNeutralised(string input, string expected)
    {
        var bytes = ReportCsv.Encode(["Value"], new[] { new object?[] { input } });
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("\"Value\"\r\n\"" + expected + "\"\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Theory]
    [InlineData("Comma, value", "Comma, value")]
    [InlineData("He said \"yes\"", "He said \"\"yes\"\"")]
    [InlineData("Two\r\nlines", "Two\r\nlines")]
    public void Encode_EscapesQuotesWithoutLosingEmbeddedDelimiters(string input, string escaped)
    {
        var csv = Encoding.UTF8.GetString(ReportCsv.Encode(["Header"], new[] { new object?[] { input } }));
        Assert.Equal("\uFEFF\"Header\"\r\n\"" + escaped + "\"\r\n", csv);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void Encode_RealAmountsKeepInvariantDecimalSeparatorsAndNegativeValues(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var csv = Encoding.UTF8.GetString(ReportCsv.Encode(["Amount", "Refund", "Missing"],
                new[] { new object?[] { 9000.25m, -125.50m, null } }));
            Assert.Equal("\uFEFF\"Amount\",\"Refund\",\"Missing\"\r\n\"9000.25\",\"-125.50\",\"\"\r\n", csv);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Encode_DatesAreExplicitUtcWithRoundTripPrecision(DateTimeKind kind)
    {
        var timestamp = DateTime.SpecifyKind(new DateTime(2030, 1, 2, 3, 4, 5), kind);
        var csv = Encoding.UTF8.GetString(ReportCsv.Encode(["Time"], new[] { new object?[] { timestamp } }));
        Assert.Equal("\uFEFF\"Time\"\r\n\"2030-01-02T03:04:05.0000000Z\"\r\n", csv);
    }

    [Fact]
    public void Maintenance_UnavailableSource_ThrowsInsteadOfFabricatingEmptySuccess()
    {
        var report = new MaintenanceReportResponse(DateTime.UnixEpoch, [], new(false, "Synthetic dependency failure", []));
        Assert.Throws<TimeoutException>(() => ReportCsv.Maintenance(report));
    }

    [Theory]
    [InlineData(9999, false)]
    [InlineData(10000, false)]
    [InlineData(10001, true)]
    public void Maintenance_RowLimit_IncludesExactBoundary(int count, bool shouldReject)
    {
        var row = new MaintenanceVehicleRow(Guid.Empty, "QA", "Test", "Car", "Test hub", 1, VehicleStatus.Maintenance, null);
        var report = new MaintenanceReportResponse(DateTime.UnixEpoch, Enumerable.Repeat(row, count).ToArray(), new(true, null, []));
        if (shouldReject) Assert.Throws<ArgumentException>(() => ReportCsv.Maintenance(report));
        else
        {
            var csv = Encoding.UTF8.GetString(ReportCsv.Maintenance(report));
            Assert.Equal(count + 1, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        }
    }

    [Fact]
    public void Maintenance_CombinedCountsCannotOverflowBeforeLimitCheck()
    {
        var vehicles = new Mock<IReadOnlyList<MaintenanceVehicleRow>>(MockBehavior.Strict);
        var activities = new Mock<IReadOnlyList<MaintenanceRecordRow>>(MockBehavior.Strict);
        vehicles.SetupGet(v => v.Count).Returns(int.MaxValue);
        activities.SetupGet(v => v.Count).Returns(int.MaxValue);
        var report = new MaintenanceReportResponse(DateTime.UnixEpoch, vehicles.Object, new(true, null, activities.Object));
        Assert.Throws<ArgumentException>(() => ReportCsv.Maintenance(report));
        vehicles.VerifyGet(v => v.Count, Times.Once);
        activities.VerifyGet(v => v.Count, Times.Once);
        vehicles.VerifyNoOtherCalls();
        activities.VerifyNoOtherCalls();
    }

    [Fact]
    public void Bookings_MapsActualStatusAmountAndIdentifiersToTheRightColumns()
    {
        var row = new BookingReportRow(Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"), Guid.Parse("33333333-3333-4333-8333-333333333333"),
            "QA-001", "Toyota", "Corolla", BookingStatus.Cancelled, DateTime.UnixEpoch,
            DateTime.UnixEpoch.AddDays(1), 18000.50m, DateTime.UnixEpoch, null);
        var csv = Encoding.UTF8.GetString(ReportCsv.Bookings(new(DateTime.UnixEpoch, [row])));
        Assert.Contains("\"License plate\",\"Make\",\"Model\",\"Status\"", csv);
        Assert.Contains("\"QA-001\",\"Toyota\",\"Corolla\",\"Cancelled\"", csv);
        Assert.Contains("\"18000.50\",\"1970-01-01T00:00:00.0000000Z\",\"\"\r\n", csv);
        Assert.Contains(row.BookingId.ToString(), csv);
        Assert.Contains(row.CustomerId.ToString(), csv);
        Assert.Contains(row.VehicleId.ToString(), csv);
    }
}
