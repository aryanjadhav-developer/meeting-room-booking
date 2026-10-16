using System.Text;
using CabinBooking.Application.Dtos;

namespace CabinBooking.Application.Common;

public static class CsvBuilder
{
    public static byte[] BuildBookingsCsv(IEnumerable<BookingDto> bookings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Cabin,BookedBy,BookedByEmail,Title,Purpose,PurposeDetail,StartTime,EndTime,DurationMinutes,Status,Phase,CheckedInAt,CreatedAt");
        foreach (var b in bookings)
        {
            sb.Append(Escape(b.Id.ToString())).Append(',')
              .Append(Escape(b.CabinName)).Append(',')
              .Append(Escape(b.BookedByName)).Append(',')
              .Append(Escape(b.BookedByEmail)).Append(',')
              .Append(Escape(b.Title)).Append(',')
              .Append(Escape(b.Purpose.ToString())).Append(',')
              .Append(Escape(b.PurposeDetail ?? "")).Append(',')
              .Append(Escape(b.StartTime.ToString("o"))).Append(',')
              .Append(Escape(b.EndTime.ToString("o"))).Append(',')
              .Append(b.DurationMinutes).Append(',')
              .Append(Escape(b.Status.ToString())).Append(',')
              .Append(Escape(b.Phase.ToString())).Append(',')
              .Append(Escape(b.CheckedInAt?.ToString("o") ?? "")).Append(',')
              .Append(Escape(b.CreatedAt.ToString("o"))).AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] BuildUtilizationCsv(UtilizationReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Utilization Report from {report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("Cabin Utilization Summary");
        sb.AppendLine("CabinId,CabinName,BookingCount,BookedHours,AvailableHours,UtilizationPercent,NoShowCount,CancelledCount");
        foreach (var c in report.Cabins)
        {
            sb.Append(c.CabinId).Append(',')
              .Append(Escape(c.CabinName)).Append(',')
              .Append(c.BookingCount).Append(',')
              .Append(c.BookedHours.ToString("F1")).Append(',')
              .Append(c.AvailableHours.ToString("F1")).Append(',')
              .Append(c.UtilizationPercent.ToString("F1")).Append(',')
              .Append(c.NoShowCount).Append(',')
              .Append(c.CancelledCount).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Top Users");
        sb.AppendLine("UserId,Name,Email,BookingCount,BookedHours");
        foreach (var u in report.TopUsers)
        {
            sb.Append(Escape(u.UserId.ToString())).Append(',')
              .Append(Escape(u.Name)).Append(',')
              .Append(Escape(u.Email)).Append(',')
              .Append(u.BookingCount).Append(',')
              .Append(u.BookedHours.ToString("F1")).AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Hourly Load");
        sb.AppendLine("Hour,BookingCount");
        foreach (var h in report.HourlyLoad)
        {
            sb.Append(h.Hour).Append(',').Append(h.BookingCount).AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
