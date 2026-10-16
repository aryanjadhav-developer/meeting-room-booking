using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> GetAll(CancellationToken ct) => users.GetAllAsync(ct);

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);
}

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Cabin utilisation between two local office dates (inclusive).</summary>
    [HttpGet("utilization")]
    public Task<UtilizationReportDto> Utilization([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reports.GetUtilizationAsync(from, to, ct);
}

[ApiController]
[Route("api/config")]
[Authorize]
public class ConfigController(BookingRuleValidator rules) : ControllerBase
{
    /// <summary>Booking rules so the UI can mirror server-side validation (office hours, durations, ...).</summary>
    [HttpGet("rules")]
    public BookingRulesDto Rules() => rules.ToDto();
}
