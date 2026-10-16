using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(BookingService bookings) : ControllerBase
{
    /// <summary>All non-cancelled bookings overlapping [from, to), optionally for one cabin.</summary>
    [HttpGet]
    public Task<IReadOnlyList<BookingDto>> GetRange([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] int? cabinId, CancellationToken ct) =>
        bookings.GetRangeAsync(from, to, cabinId, ct);

    [HttpGet("mine")]
    public Task<IReadOnlyList<BookingDto>> Mine([FromQuery] bool includePast, CancellationToken ct) => bookings.GetMineAsync(includePast, ct);

    [HttpGet("{id:guid}")]
    public Task<BookingDto> Get(Guid id, CancellationToken ct) => bookings.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    /// <summary>Book a cabin starting right now.</summary>
    [HttpPost("quick")]
    public async Task<ActionResult<BookingDto>> QuickBook(QuickBookRequest request, CancellationToken ct)
    {
        var booking = await bookings.QuickBookAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    [HttpPut("{id:guid}")]
    public Task<BookingDto> Update(Guid id, UpdateBookingRequest request, CancellationToken ct) => bookings.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/extend")]
    public Task<BookingDto> Extend(Guid id, ExtendBookingRequest request, CancellationToken ct) => bookings.ExtendAsync(id, request.Minutes, ct);

    [HttpPost("{id:guid}/end")]
    public Task<BookingDto> End(Guid id, CancellationToken ct) => bookings.EndEarlyAsync(id, ct);

    [HttpPost("{id:guid}/check-in")]
    public Task<BookingDto> CheckIn(Guid id, CancellationToken ct) => bookings.CheckInAsync(id, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await bookings.CancelAsync(id, ct);
        return NoContent();
    }
}
