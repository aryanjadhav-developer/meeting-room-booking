using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class ReportService(IAppDbContext db, BookingRuleValidator rules)
{
    public async Task<UtilizationReportDto> GetUtilizationAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) throw new AppValidationException("'to' must not be before 'from'.");
        if (to.DayNumber - from.DayNumber > 366) throw new AppValidationException("Please request at most one year.");

        var (rangeStart, _) = rules.DayWindowUtc(from);
        var (_, rangeEnd) = rules.DayWindowUtc(to);

        var cabins = await db.Cabins.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var allBookings = await db.Bookings.AsNoTracking()
            .Include(b => b.BookedBy)
            .ToListAsync(ct);
        var bookings = allBookings
            .Where(b => b.StartTime >= rangeStart && b.StartTime < rangeEnd)
            .ToList();

        var workingDays = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(i => from.AddDays(i))
            .Count(rules.IsWorkingDay);
        var officeHours = (rules.Rules.OfficeEnd - rules.Rules.OfficeStart).TotalHours;
        var availableHours = workingDays * officeHours;

        var held = bookings.Where(b => b.Status == BookingStatus.Confirmed).ToList();

        var perCabin = cabins.Select(c =>
        {
            var cb = bookings.Where(b => b.CabinId == c.Id).ToList();
            var hours = cb.Where(b => b.Status == BookingStatus.Confirmed).Sum(b => b.Duration.TotalHours);
            return new CabinUtilizationDto(
                c.Id, c.Name,
                cb.Count(b => b.Status == BookingStatus.Confirmed),
                Math.Round(hours, 2),
                availableHours,
                availableHours > 0 ? Math.Round(hours / availableHours * 100, 1) : 0,
                cb.Count(b => b.Status == BookingStatus.NoShow),
                cb.Count(b => b.Status == BookingStatus.Cancelled));
        }).ToList();

        var hourly = Enumerable.Range(rules.Rules.OfficeStart.Hour, Math.Max(1, (int)Math.Ceiling(officeHours)))
            .Select(h => new HourlyLoadDto(h, held.Count(b => rules.ToLocal(b.StartTime).Hour == h)))
            .ToList();

        var topUsers = held
            .GroupBy(b => b.BookedByUserId)
            .Select(g => new TopUserDto(g.Key, g.First().BookedBy.Name, g.First().BookedBy.Email, g.Count(), Math.Round(g.Sum(b => b.Duration.TotalHours), 2)))
            .OrderByDescending(u => u.BookedHours)
            .Take(10)
            .ToList();

        return new UtilizationReportDto(from, to, perCabin, hourly, topUsers);
    }
}
