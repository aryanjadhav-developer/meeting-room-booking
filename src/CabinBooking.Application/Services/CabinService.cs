using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class CabinService(IAppDbContext db, IBookingNotifier notifier, BookingRuleValidator rules, IAuditLogger audit)
{
    public async Task<IReadOnlyList<CabinDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Cabins.AsNoTracking();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        var cabins = await query.OrderBy(c => c.Name).ToListAsync(ct);
        return cabins.Select(c => c.ToDto()).ToList();
    }

    public async Task<CabinDto> GetAsync(int id, CancellationToken ct) =>
        (await db.Cabins.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cabin not found.")).ToDto();

    /// <summary>Live board: is each cabin occupied right now, and what is next today.</summary>
    public async Task<IReadOnlyList<CabinStatusDto>> GetStatusesAsync(CancellationToken ct)
    {
        var now = rules.Now;
        var (_, endOfDay) = rules.DayWindowUtc(rules.Today);

        var cabins = await db.Cabins.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Cabin).Include(b => b.BookedBy)
            .Where(b => b.Status == BookingStatus.Confirmed && b.EndTime > now && b.StartTime < endOfDay)
            .OrderBy(b => b.StartTime)
            .ToListAsync(ct);

        return cabins.Select(c =>
        {
            var mine = bookings.Where(b => b.CabinId == c.Id).ToList();
            var current = mine.FirstOrDefault(b => b.StartTime <= now);
            var next = mine.FirstOrDefault(b => b.StartTime > now);
            return new CabinStatusDto(c.ToDto(), current is not null, current?.ToDto(now), next?.ToDto(now));
        }).ToList();
    }

    public async Task<CabinDto> CreateAsync(UpsertCabinRequest request, CancellationToken ct)
    {
        Validate(request);
        if (await db.Cabins.AnyAsync(c => c.Name == request.Name.Trim(), ct))
            throw new ConflictException($"A cabin named '{request.Name.Trim()}' already exists.");

        var cabin = new Cabin();
        Apply(cabin, request);
        db.Cabins.Add(cabin);
        await db.SaveChangesAsync(ct);
        await notifier.CabinsChangedAsync();
        await audit.LogAsync("CabinCreated", "Cabin", cabin.Id.ToString(), $"Created room '{cabin.Name}' (Capacity: {cabin.Capacity})", ct);
        return cabin.ToDto();
    }

    public async Task<CabinDto> UpdateAsync(int id, UpsertCabinRequest request, CancellationToken ct)
    {
        Validate(request);
        var cabin = await db.Cabins.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cabin not found.");
        if (await db.Cabins.AnyAsync(c => c.Id != id && c.Name == request.Name.Trim(), ct))
            throw new ConflictException($"A cabin named '{request.Name.Trim()}' already exists.");

        Apply(cabin, request);
        await db.SaveChangesAsync(ct);
        await notifier.CabinsChangedAsync();
        await audit.LogAsync("CabinUpdated", "Cabin", cabin.Id.ToString(), $"Updated room '{cabin.Name}' (Active: {cabin.IsActive})", ct);
        return cabin.ToDto();
    }

    private static void Apply(Cabin cabin, UpsertCabinRequest r)
    {
        cabin.Name = r.Name.Trim();
        cabin.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        cabin.Capacity = r.Capacity;
        cabin.CapacityLabel = string.IsNullOrWhiteSpace(r.CapacityLabel) ? null : r.CapacityLabel.Trim();
        cabin.Amenities = string.Join(',', r.Amenities.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct());
        cabin.Color = r.Color;
        cabin.IsActive = r.IsActive;
    }

    private static void Validate(UpsertCabinRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 60)
            throw new AppValidationException("Cabin name is required (max 60 characters).");
        if (r.Capacity is < 1 or > 100)
            throw new AppValidationException("Capacity must be between 1 and 100.");
        if (r.CapacityLabel?.Trim().Length > 20)
            throw new AppValidationException("Capacity label can be at most 20 characters (e.g. \"9+\" or \"3-4\").");
        if (!System.Text.RegularExpressions.Regex.IsMatch(r.Color ?? "", "^#[0-9a-fA-F]{6}$"))
            throw new AppValidationException("Colour must be a hex value like #1976d2.");
    }
}
