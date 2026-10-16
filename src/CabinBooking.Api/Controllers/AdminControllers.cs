using CabinBooking.Application.Common;
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

    /// <summary>Export cabin utilisation report as CSV.</summary>
    [HttpGet("utilization/csv")]
    public async Task<IActionResult> UtilizationCsv([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var data = await reports.GetUtilizationAsync(from, to, ct);
        var bytes = CsvBuilder.BuildUtilizationCsv(data);
        var fileName = $"utilization_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}

[ApiController]
[Route("api/config")]
[Authorize]
public class ConfigController(BookingRuleValidator rules) : ControllerBase
{
    /// <summary>Booking rules so the UI can mirror server-side validation (office hours, durations, ...).</summary>
    [HttpGet("rules")]
    public BookingRulesDto Rules() => rules.ToDto();

    /// <summary>Update system booking rules and office hours.</summary>
    [HttpPut("rules")]
    [Authorize(Roles = "Admin")]
    public Task<BookingRulesDto> UpdateRules([FromBody] UpdateSettingsRequest request, [FromServices] SettingsService settings, CancellationToken ct) =>
        settings.UpdateSettingsAsync(request, ct);
}

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Admin")]
public class AuditController(AuditService audit) : ControllerBase
{
    /// <summary>Get recent audit log entries.</summary>
    [HttpGet]
    public Task<IReadOnlyList<AuditEntryDto>> Get([FromQuery] int take = 50, [FromQuery] int skip = 0, CancellationToken ct = default) =>
        audit.GetEntriesAsync(take, skip, ct);
}
