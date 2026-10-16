using System.Text.Json;
using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class SettingsService(
    IAppDbContext db,
    BookingRuleValidator rules,
    IAuditLogger audit)
{
    private const string SettingKey = "BookingRules";

    public async Task InitializeFromDatabaseAsync(CancellationToken ct = default)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            try
            {
                var req = JsonSerializer.Deserialize<UpdateSettingsRequest>(setting.Value);
                if (req != null)
                {
                    rules.ApplyUpdate(req);
                }
            }
            catch
            {
                // Fall back to config if parse fails
            }
        }
    }

    public async Task<BookingRulesDto> UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken ct = default)
    {
        rules.ApplyUpdate(request);

        var json = JsonSerializer.Serialize(request);
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (existing == null)
        {
            db.Settings.Add(new SystemSetting
            {
                Key = SettingKey,
                Value = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Value = json;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SettingsUpdated", "SystemSetting", SettingKey, "Updated booking rules and office hours", ct);

        return rules.ToDto();
    }
}
