using CabinBooking.Domain.Entities;

namespace CabinBooking.Application.Dtos;

public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive);

    public static CabinDto ToDto(this Cabin c) => new(
        c.Id, c.Name, c.Description, c.Capacity,
        c.Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        c.Color, c.IsActive, c.CapacityLabel);

    /// <summary>Requires <see cref="Booking.Cabin"/> and <see cref="Booking.BookedBy"/> to be loaded.</summary>
    public static BookingDto ToDto(this Booking b, DateTimeOffset now) => new(
        b.Id, b.CabinId, b.Cabin.Name,
        b.BookedByUserId, b.BookedBy.Name, b.BookedBy.Email,
        b.Title, b.Purpose, b.PurposeDetail, b.Notes,
        b.StartTime, b.EndTime, (int)Math.Round(b.Duration.TotalMinutes),
        b.Status, b.GetPhase(now), b.CheckedInAt, b.CreatedAt);
}
