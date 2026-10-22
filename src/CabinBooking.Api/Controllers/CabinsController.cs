using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/cabins")]
[Authorize]
public class CabinsController(CabinService cabins, BookingService bookings) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CabinDto>> GetAll([FromQuery] bool includeInactive, CancellationToken ct) =>
        cabins.GetAllAsync(includeInactive, ct);

    /// <summary>Live status of every active cabin: occupied now, current and next booking today.</summary>
    [HttpGet("status")]
    public Task<IReadOnlyList<CabinStatusDto>> Status(CancellationToken ct) => cabins.GetStatusesAsync(ct);

    [HttpGet("{id:int}")]
    public Task<CabinDto> Get(int id, CancellationToken ct) => cabins.GetAsync(id, ct);

    /// <summary>Free time ranges for a cabin on a local office date.</summary>
    [HttpGet("{id:int}/availability")]
    public Task<AvailabilityDto> Availability(int id, [FromQuery] DateOnly date, CancellationToken ct) =>
        bookings.GetAvailabilityAsync(id, date, ct);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CabinDto>> Create(UpsertCabinRequest request, CancellationToken ct)
    {
        var cabin = await cabins.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = cabin.Id }, cabin);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<CabinDto> Update(int id, UpsertCabinRequest request, CancellationToken ct) => cabins.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await cabins.DeleteAsync(id, ct);
        return NoContent();
    }
}
