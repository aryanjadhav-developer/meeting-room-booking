using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

public class Phase5FeatureTests(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_audit_log_records_booking_operations()
    {
        var (admin, alice, _) = await SetupUsersAsync();

        // 1. Create booking
        var booking = await BookOkAsync(alice, 1, At(11), At(12));

        // 2. Update booking
        var updateRes = await alice.Http.PutAsJsonAsync($"/api/bookings/{booking.Id}",
            new UpdateBookingRequest(1, "Updated Project Sync", BookingPurpose.Meeting, "Notes", At(11), At(12), null));
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 3. Cancel booking
        var cancelRes = await alice.Http.DeleteAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // 4. Query audit log as admin
        var auditRes = await admin.Http.GetAsync("/api/audit");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var entries = await ReadAsync<IReadOnlyList<AuditEntryDto>>(auditRes);

        Assert.Contains(entries, e => e.Action == "BookingCreated" && e.EntityId == booking.Id.ToString());
        Assert.Contains(entries, e => e.Action == "BookingUpdated" && e.EntityId == booking.Id.ToString());
        Assert.Contains(entries, e => e.Action == "BookingCancelled" && e.EntityId == booking.Id.ToString());
    }

    [Fact]
    public async Task TC02_audit_log_records_cabin_and_user_updates()
    {
        var (admin, alice, _) = await SetupUsersAsync();

        // Create cabin
        var createCabinRes = await admin.Http.PostAsJsonAsync("/api/cabins",
            new UpsertCabinRequest("Audit Test Room", "Description", 8, ["TV"], "#336699", true));
        Assert.Equal(HttpStatusCode.Created, createCabinRes.StatusCode);
        var cabin = await ReadAsync<CabinDto>(createCabinRes);

        // Update user
        var updateUserRes = await admin.Http.PutAsJsonAsync($"/api/users/{alice.User.Id}",
            new UpdateUserRequest(UserRole.Employee, IsActive: true));
        Assert.Equal(HttpStatusCode.OK, updateUserRes.StatusCode);

        // Verify audit log
        var auditRes = await admin.Http.GetAsync("/api/audit");
        var entries = await ReadAsync<IReadOnlyList<AuditEntryDto>>(auditRes);

        Assert.Contains(entries, e => e.Action == "CabinCreated" && e.EntityId == cabin.Id.ToString());
        Assert.Contains(entries, e => e.Action == "UserUpdated" && e.EntityId == alice.User.Id.ToString());
    }

    [Fact]
    public async Task TC03_audit_log_endpoint_requires_admin()
    {
        var (_, alice, _) = await SetupUsersAsync();
        var res = await alice.Http.GetAsync("/api/audit");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public void TC04_ics_calendar_builder_generates_valid_format()
    {
        var bookingId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var end = start.AddHours(1);

        var ics = IcsCalendarBuilder.BuildIcs(bookingId, "Client Demo", "Conference Room",
            "noreply@meetingrooms.local", "alice@example.test", start, end, "Agenda items", "REQUEST");

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("METHOD:REQUEST", ics);
        Assert.Contains("SUMMARY:Client Demo", ics);
        Assert.Contains("LOCATION:Conference Room", ics);
        Assert.Contains($"UID:{bookingId}@meetingrooms.local", ics);
        Assert.Contains("END:VCALENDAR", ics);
    }

    [Fact]
    public async Task TC05_admin_updates_settings_persists_and_applies()
    {
        var (admin, alice, _) = await SetupUsersAsync();

        // Employee cannot update settings
        var forbiddenRes = await alice.Http.PutAsJsonAsync("/api/config/rules", new UpdateSettingsRequest(
            new TimeOnly(8, 0), new TimeOnly(19, 0), [DayOfWeek.Monday, DayOfWeek.Tuesday], 15, 15, 120, 14, 15, true, 10, ["Alpha", "Beta"]));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // Admin updates settings
        var updateRes = await admin.Http.PutAsJsonAsync("/api/config/rules", new UpdateSettingsRequest(
            new TimeOnly(8, 30), new TimeOnly(19, 30), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday], 15, 15, 180, 20, 15, true, 10, ["Project Alpha", "Project Beta"]));
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedRules = await ReadAsync<BookingRulesDto>(updateRes);

        Assert.Equal(new TimeOnly(8, 30), updatedRules.OfficeStart);
        Assert.Equal(new TimeOnly(19, 30), updatedRules.OfficeEnd);
        Assert.Equal(20, updatedRules.MaxDaysAhead);
        Assert.Contains("Project Alpha", updatedRules.Projects);

        // GET rules reflects the update
        var getRes = await alice.Http.GetAsync("/api/config/rules");
        var activeRules = await ReadAsync<BookingRulesDto>(getRes);
        Assert.Equal(new TimeOnly(8, 30), activeRules.OfficeStart);
    }

    [Fact]
    public async Task TC06_csv_export_bookings()
    {
        var (_, alice, _) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(11), At(12));

        var res = await alice.Http.GetAsync("/api/bookings/export/csv");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", res.Content.Headers.ContentType?.ToString());

        var csv = await res.Content.ReadAsStringAsync();
        Assert.Contains("Cabin,BookedBy", csv);
        Assert.Contains("Lunch Room", csv);
    }

    [Fact]
    public async Task TC07_csv_export_utilization_report()
    {
        var (admin, _, _) = await SetupUsersAsync();

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var res = await admin.Http.GetAsync($"/api/reports/utilization/csv?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", res.Content.Headers.ContentType?.ToString());

        var csv = await res.Content.ReadAsStringAsync();
        Assert.Contains("Utilization Report", csv);
        Assert.Contains("Cabin Utilization Summary", csv);
    }
}
