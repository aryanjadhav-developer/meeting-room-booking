using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CabinBooking.Api.Background;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using CabinBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace CabinBooking.IntegrationTests.Infrastructure;

public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed record Session(HttpClient Http, UserDto User, string Token);

/// <summary>
/// Hosts the real API in-process against a dedicated SQL Server database (CabinBookingDb_Tests),
/// with a controllable clock. The no-show background job is removed; tests trigger it explicitly.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static MsSqlContainer? _msSqlContainer;
    public const string Password = "Passw0rd!test";
    public const string AdminEmail = "admin@example.test";
    public static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    /// <summary>Wednesday 21 Oct 2026, 10:00 IST: a working day inside office hours.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 21, 10, 0, 0, Ist);

    public static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        new DateTimeOffset(2026, 10, 21, hour, minute, 0, Ist).AddDays(dayOffset);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public FakeClock Clock { get; } = new(Start);

    static TestApp()
    {
        // Environment variables override appsettings.json and are visible before the host is built.
        var conn = Environment.GetEnvironmentVariable("CABINBOOKING_TEST_DB")
                   ?? "Server=localhost;Database=CabinBookingDb_Tests;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable("ConnectionStrings__CabinBooking", conn);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", new string('t', 64));
        Environment.SetEnvironmentVariable("Database__Provider", "SqlServer");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("BookingRules__AutoReleaseEnabled", "true");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Admin__Email", AdminEmail);
        Environment.SetEnvironmentVariable("Admin__Password", Password);
        Environment.SetEnvironmentVariable("Admin__Name", "Admin User");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            var job = services.Single(d => d.ImplementationType == typeof(NoShowReleaseService));
            services.Remove(job);

            // Tokens are issued with the fake clock; don't let the real clock reject them.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                o => o.TokenValidationParameters.ValidateLifetime = false);
        });
    }

    /// <summary>Empty users/bookings, restore the 3 seeded cabins, reset the clock.</summary>
    public async Task ResetAsync()
    {
        Clock.Now = Start;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM Bookings;
            DELETE FROM Users;
            DELETE FROM Cabins WHERE Id > 3;
            UPDATE Cabins SET IsActive = 1,
                Name = CASE Id WHEN 1 THEN 'Lunch Room' WHEN 2 THEN 'Conference Room' ELSE 'Cabin 3' END,
                CapacityLabel = CASE Id WHEN 1 THEN '9+' WHEN 2 THEN '5+' ELSE '3-4' END;
            """);
        await AdminSeeder.SeedAdminAsync(scope.ServiceProvider);
    }

    public async Task InitializeAsync()
    {
        var existing = Environment.GetEnvironmentVariable("CABINBOOKING_TEST_DB");
        if (string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                    .Build();
                await _msSqlContainer.StartAsync();
                Environment.SetEnvironmentVariable("ConnectionStrings__CabinBooking", _msSqlContainer.GetConnectionString());
            }
            catch
            {
                _msSqlContainer = null;
            }
        }
    }

    public new async Task DisposeAsync()
    {
        if (_msSqlContainer is not null)
        {
            await _msSqlContainer.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    public async Task<Session> LoginAsync(string email, string password = Password)
    {
        var http = CreateClient();
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new Session(http, auth.User, auth.Token);
    }

    public Task<Session> LoginAdminAsync() => LoginAsync(AdminEmail, Password);

    public async Task<Session> RegisterAsync(string name)
    {
        var http = CreateClient();
        var email = $"{name.ToLowerInvariant().Replace(' ', '.')}@example.test";
        var res = await http.PostAsJsonAsync("/api/auth/register", new { name, email, password = Password }, Json);
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new Session(http, auth.User, auth.Token);
    }

    public async Task<int> ReleaseNoShowsAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BookingService>().ReleaseNoShowsAsync(CancellationToken.None);
    }

    public HubConnection CreateHubConnection(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/hubs/bookings"), o =>
            {
                o.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<TestApp>
{
    public const string Name = "api";
}

/// <summary>Base class: resets the database before every test and offers small HTTP helpers.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTest(TestApp app) : IAsyncLifetime
{
    protected TestApp App { get; } = app;
    protected static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) => TestApp.At(hour, minute, dayOffset);

    public Task InitializeAsync() => App.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Logs in the seeded admin plus registers the given employees.</summary>
    protected async Task<(Session Admin, Session Alice, Session Bob)> SetupUsersAsync()
    {
        var admin = await App.LoginAdminAsync();
        var alice = await App.RegisterAsync("Alice");
        var bob = await App.RegisterAsync("Bob");
        return (admin, alice, bob);
    }

    protected static Task<HttpResponseMessage> BookAsync(Session s, int cabinId, DateTimeOffset start, DateTimeOffset end, string title = "Team sync") =>
        s.Http.PostAsJsonAsync("/api/bookings", new CreateBookingRequest(cabinId, title, Domain.Enums.BookingPurpose.Meeting, null, start, end), TestApp.Json);

    protected static async Task<BookingDto> BookOkAsync(Session s, int cabinId, DateTimeOffset start, DateTimeOffset end, string title = "Team sync")
    {
        var res = await BookAsync(s, cabinId, start, end, title);
        Assert.Equal(System.Net.HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<BookingDto>(TestApp.Json))!;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<T>(TestApp.Json))!;

    protected static async Task<string> DetailAsync(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString() ?? "";

    protected static Task<HttpResponseMessage> PostAsync(Session s, string url, object? body = null) =>
        s.Http.PostAsJsonAsync(url, body ?? new { }, TestApp.Json);
}
