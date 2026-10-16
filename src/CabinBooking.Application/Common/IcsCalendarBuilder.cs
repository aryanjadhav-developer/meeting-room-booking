namespace CabinBooking.Application.Common;

public static class IcsCalendarBuilder
{
    public static string BuildIcs(
        Guid bookingId,
        string title,
        string cabinName,
        string organizerEmail,
        string attendeeEmail,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? notes,
        string method = "REQUEST")
    {
        var startUtc = startTime.UtcDateTime.ToString("yyyyMMdd\\THHmmss\\Z");
        var endUtc = endTime.UtcDateTime.ToString("yyyyMMdd\\THHmmss\\Z");
        var nowUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd\\THHmmss\\Z");
        var summary = Escape(title);
        var location = Escape(cabinName);
        var description = Escape(notes ?? $"Booking for {cabinName}");
        var status = method == "CANCEL" ? "CANCELLED" : "CONFIRMED";

        return $"""
            BEGIN:VCALENDAR
            PRODID:-//Meeting Room Booking//EN
            VERSION:2.0
            CALSCALE:GREGORIAN
            METHOD:{method}
            BEGIN:VEVENT
            DTSTAMP:{nowUtc}
            UID:{bookingId}@meetingrooms.local
            DTSTART:{startUtc}
            DTEND:{endUtc}
            SUMMARY:{summary}
            DESCRIPTION:{description}
            LOCATION:{location}
            STATUS:{status}
            ORGANIZER;CN=Meeting Room Booking:mailto:{organizerEmail}
            ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;CN={attendeeEmail}:mailto:{attendeeEmail}
            END:VEVENT
            END:VCALENDAR
            """.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n").Replace("\r", "");
}
