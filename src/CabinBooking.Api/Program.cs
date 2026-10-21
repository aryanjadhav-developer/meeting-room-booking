using System.Text.Json.Serialization;
using CabinBooking.Api.Auth;
using CabinBooking.Api.Background;
using CabinBooking.Api.Errors;
using CabinBooking.Api.Filters;
using CabinBooking.Api.Realtime;
using CabinBooking.Application;
using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Options;
using CabinBooking.Application.Services;
using CabinBooking.Infrastructure;
using CabinBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "cabinbooking-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

// A Windows service starts in C:\Windows\System32, and a published exe may be started from anywhere, so point the
// content root (appsettings, wwwroot) at the app folder. Under "dotnet run" the project folder stays the content root.
var isPublishedLayout = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() || isPublishedLayout ? AppContext.BaseDirectory : default
});
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "cabinbooking-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));
builder.Host.UseWindowsService(o => o.ServiceName = "CabinBooking");

// ---------- Options ----------
builder.Services.Configure<BookingRulesOptions>(builder.Configuration.GetSection(BookingRulesOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");

// ---------- Layers ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IBookingNotifier, SignalRBookingNotifier>();
builder.Services.AddHostedService<NoShowReleaseService>();
builder.Services.AddHostedService<EmailBackgroundDispatcher>();
builder.Services.AddHostedService<RecurringBookingScheduler>();
builder.Services.AddHostedService<ReminderService>();

// ---------- Auth ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.GetKey(),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        // Browsers can't set headers on WebSocket connections, so SignalR sends the token in the query string.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = AccountStatusCheck.OnTokenValidated,
            OnChallenge = AccountStatusCheck.OnChallenge
        };
    });
builder.Services.AddAuthorization();

// ---------- Web ----------
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationFilter>();
})
.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Meeting Room / Cabin Booking API",
        Version = "v1",
        Description = "Real-time meeting room and cabin booking system with advisory locks, Identity authentication, SignalR, and dynamic policies."
    });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });

    var apiXml = Path.Combine(AppContext.BaseDirectory, "CabinBooking.Api.xml");
    if (File.Exists(apiXml))
    {
        c.IncludeXmlComments(apiXml, includeControllerXmlComments: true);
    }
    var appXml = Path.Combine(AppContext.BaseDirectory, "CabinBooking.Application.xml");
    if (File.Exists(appXml))
    {
        c.IncludeXmlComments(appXml);
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
    allowedOrigins = ["https://meeting-room-booking-fjrr.onrender.com"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var rateLimitingEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 30),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });
}
else
{
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("auth", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none"));
    });
}

var app = builder.Build();

// ---------- Database ----------
if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsSqlite())
    {
        await db.Database.EnsureCreatedAsync();
        // EnsureCreatedAsync only creates the DB if it does not exist at all; it never
        // adds tables that were introduced in later versions. Run explicit CREATE TABLE
        // IF NOT EXISTS statements so new tables are created on existing databases too.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecurringBookings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RecurringBookings" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "CabinId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Purpose" TEXT NOT NULL,
                "PurposeDetail" TEXT NULL,
                "Notes" TEXT NULL,
                "StartDate" TEXT NOT NULL,
                "EndDate" TEXT NOT NULL,
                "StartMinuteOfDay" INTEGER NOT NULL,
                "DurationMinutes" INTEGER NOT NULL,
                "DaysOfWeekJson" TEXT NOT NULL,
                "AttendeeEmailsJson" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_RecurringBookings_Cabins_CabinId"
                    FOREIGN KEY ("CabinId") REFERENCES "Cabins" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_RecurringBookings_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_RecurringBookings_CabinId"
                ON "RecurringBookings" ("CabinId");
            CREATE INDEX IF NOT EXISTS "IX_RecurringBookings_UserId_IsActive"
                ON "RecurringBookings" ("UserId", "IsActive");

            CREATE TABLE IF NOT EXISTS "RecurringBookingInstances" (
                "RecurringBookingId" TEXT NOT NULL,
                "Date" TEXT NOT NULL,
                "BookingId" TEXT NULL,
                CONSTRAINT "PK_RecurringBookingInstances"
                    PRIMARY KEY ("RecurringBookingId", "Date"),
                CONSTRAINT "FK_RecurringBookingInstances_RecurringBookings_RecurringBookingId"
                    FOREIGN KEY ("RecurringBookingId") REFERENCES "RecurringBookings" ("Id") ON DELETE CASCADE
            );
            """);
        // Add ReminderSentAt column to Bookings if it was not there yet.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Bookings" ADD COLUMN "ReminderSentAt" TEXT NULL;""");
        }
        catch { /* column already exists — safe to ignore */ }
    }
    else
    {
        await db.Database.MigrateAsync();
    }
    await AdminSeeder.SeedAdminAsync(scope.ServiceProvider);
    await DemoSeeder.SeedDemoAsync(scope.ServiceProvider);
    var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
    await settingsService.InitializeFromDatabaseAsync();
    await scope.ServiceProvider.GetRequiredService<ProjectService>()
        .SeedAsync(scope.ServiceProvider.GetRequiredService<BookingRuleValidator>().ToDto().Projects, CancellationToken.None);
}

// ---------- Pipeline ----------
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https:; connect-src 'self' ws: wss:; frame-ancestors 'none';");
    }
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();   // serves the built React app from wwwroot in production
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BookingHub>(BookingHub.Path);
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program;
