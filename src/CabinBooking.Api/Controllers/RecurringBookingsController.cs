using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/recurring-bookings")]
[Authorize]
public class RecurringBookingsController(RecurringBookingService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RecurringBookingDto>> GetAll(CancellationToken ct) =>
        service.GetAllAsync(ct);

    [HttpPost]
    public Task<RecurringBookingDto> Create([FromBody] CreateRecurringBookingRequest req, CancellationToken ct) =>
        service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<RecurringBookingDto> Update(Guid id, [FromBody] UpdateRecurringBookingRequest req, CancellationToken ct) =>
        service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
