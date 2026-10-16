using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

// ============================================================== 17 Project & purpose
public class F17_ProjectAndPurpose(TestApp app) : ApiTest(app)
{
    private static object Body(string project, string? purpose, string? purposeDetail = null) => new
    {
        cabinId = 1,
        title = project,
        purpose,
        purposeDetail,
        notes = (string?)null,
        startTime = At(11),
        endTime = At(12)
    };

    [Fact]
    public async Task TC01_booking_without_purpose_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await alice.Http.PostAsJsonAsync("/api/bookings", Body("Project Alpha", null), TestApp.Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Purpose is required.", await DetailAsync(res));
    }

    [Fact]
    public async Task TC02_other_purpose_needs_a_description()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await alice.Http.PostAsJsonAsync("/api/bookings", Body("Project Alpha", "Other", "   "), TestApp.Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("describe the purpose", await DetailAsync(res));
    }

    [Fact]
    public async Task TC03_other_purpose_with_description_is_saved()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await alice.Http.PostAsJsonAsync("/api/bookings", Body("Project Beta", "Other", "Vendor demo"), TestApp.Json));
        Assert.Equal((BookingPurpose.Other, "Vendor demo", "Project Beta"), (b.Purpose, b.PurposeDetail, b.Title));
    }

    [Fact]
    public async Task TC04_description_is_ignored_for_standard_purposes()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await alice.Http.PostAsJsonAsync("/api/bookings", Body("Project Gamma", "Discussion", "leftover text"), TestApp.Json));
        Assert.Equal(BookingPurpose.Discussion, b.Purpose);
        Assert.Null(b.PurposeDetail);
    }

    [Fact]
    public async Task TC05_demo_projects_are_offered_custom_names_allowed_blank_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var rules = await alice.Http.GetFromJsonAsync<BookingRulesDto>("/api/config/rules", TestApp.Json);
        Assert.Equal(["Project Alpha", "Project Beta", "Project Gamma"], rules!.Projects);

        var custom = await ReadAsync<BookingDto>(await alice.Http.PostAsJsonAsync("/api/bookings", Body("Client Onboarding", "Meeting"), TestApp.Json));
        Assert.Equal("Client Onboarding", custom.Title);

        var blank = await alice.Http.PostAsJsonAsync("/api/bookings", Body("  ", "Meeting"), TestApp.Json);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("Project is required.", await DetailAsync(blank));
    }
}
