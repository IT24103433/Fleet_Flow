using System.Globalization;
using System.Text;
using System.Text.Json;
using FleetService.Api.Dtos;
namespace FleetService.Api.Services;

public static class ReportCsv
{
    public static byte[] Bookings(BookingExportResponse report) => Encode(
        ["Booking ID", "Customer ID", "Vehicle ID", "License plate", "Make", "Model", "Status",
         "Starts (UTC)", "Ends (UTC)", "Reserved amount (LKR)", "Created (UTC)", "Updated (UTC)"],
        report.Items.Select(r => new object?[] { r.BookingId, r.CustomerId, r.VehicleId, r.LicensePlate,
            r.Make, r.Model, r.Status.ToString(), r.StartDateTime, r.EndDateTime, r.TotalCost, r.CreatedAt, r.UpdatedAt }));

    public static byte[] Maintenance(MaintenanceReportResponse report)
    {
        if (!report.WorkOrders.Available) throw new TimeoutException("Maintenance report source unavailable.");
        if ((long)report.CurrentVehicles.Count + report.WorkOrders.Records.Count > BookingExportQuery.MaxRows)
            throw new ArgumentException("Maintenance export exceeds the supported row limit.");
        var vehicles = report.CurrentVehicles.Select(v => new object?[] { "Current vehicle", v.VehicleId,
            v.VehicleId, v.LicensePlate, v.Make, v.Model, v.HubLocation, v.Mileage, v.Status.ToString(),
            null, null, null, null, v.LastVehicleUpdateAt, null });
        var activities = report.WorkOrders.Records.Select(r => new object?[] { "Maintenance activity", r.MaintenanceId,
            r.VehicleId, r.LicensePlate, null, null, null, null, r.Status, r.Activity,
            r.ScheduledAt, r.CompletedAt, r.Cost, null, JsonSerializer.Serialize(r.History) });
        return Encode(["Record type", "Record ID", "Vehicle ID", "License plate", "Make", "Model", "Hub",
            "Mileage (km)", "Status", "Activity", "Scheduled (UTC)", "Completed (UTC)", "Recorded cost (LKR)",
            "Last vehicle update (UTC)", "History (JSON)"], vehicles.Concat(activities));
    }

    public static byte[] Encode(string[] headers, IEnumerable<object?[]> rows)
    {
        var content = new StringBuilder("\uFEFF"); // Excel-compatible UTF-8 BOM.
        void Line(IEnumerable<object?> cells) => content.AppendJoin(',', cells.Select(Cell)).Append("\r\n");
        Line(headers);
        foreach (var row in rows) Line(row);
        return Encoding.UTF8.GetBytes(content.ToString());
    }

    private static string Cell(object? value)
    {
        var text = value switch {
            null => "",
            DateTime time => (time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time.ToUniversalTime()).ToString("O", CultureInfo.InvariantCulture),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };
        if (value is string)
        {
            var first = text.SkipWhile(c => char.IsWhiteSpace(c) || char.IsControl(c)).FirstOrDefault();
            // Quoted tab prefix makes formula-looking user text literal for Excel viewing.
            // Keep actual numeric values numeric; never turn an untrusted string into a formula.
            if ("=+-@＝＋－＠".Contains(first) || text.StartsWith('\t') || text.StartsWith('\r') || text.StartsWith('\n')) text = "\t" + text;
        }
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
