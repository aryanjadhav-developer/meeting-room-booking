namespace CabinBooking.Domain.Entities;

public class Cabin
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Maximum (or typical) number of people; used for sorting and reports.</summary>
    public int Capacity { get; set; }
    /// <summary>How capacity is shown to people, e.g. "9+" or "3-4". Falls back to <see cref="Capacity"/> when empty.</summary>
    public string? CapacityLabel { get; set; }
    /// <summary>Comma separated list, e.g. "TV,Whiteboard,VC".</summary>
    public string Amenities { get; set; } = string.Empty;
    /// <summary>Hex colour used on the timeline.</summary>
    public string Color { get; set; } = "#1976d2";
    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
