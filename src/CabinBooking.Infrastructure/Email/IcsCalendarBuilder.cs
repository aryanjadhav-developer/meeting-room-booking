namespace CabinBooking.Infrastructure.Email;

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
        string method = "REQUEST") =>
        CabinBooking.Application.Common.IcsCalendarBuilder.BuildIcs(
            bookingId, title, cabinName, organizerEmail, attendeeEmail, startTime, endTime, notes, method);
}
