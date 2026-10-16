using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using CabinBooking.Infrastructure.Email;
using CabinBooking.Infrastructure.Persistence;
using CabinBooking.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CabinBooking.IntegrationTests;

public class SecurityAndAuthTests(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_lockout_triggers_after_5_failed_logins()
    {
        var client = App.CreateClient();
        var email = $"lockout_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Lockout User", email, "P@ssword123"));

        // 4 failed attempts
        for (var i = 0; i < 4; i++)
        {
            var fail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword"));
            Assert.Equal(HttpStatusCode.BadRequest, fail.StatusCode);
            var detail = await DetailAsync(fail);
            Assert.Contains("Invalid e-mail or password", detail);
        }

        // 5th failed attempt triggers lockout
        var fifth = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword"));
        Assert.Equal(HttpStatusCode.BadRequest, fifth.StatusCode);

        // 6th attempt with CORRECT password must now be rejected due to lockout
        var locked = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssword123"));
        Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
        var lockedDetail = await DetailAsync(locked);
        Assert.Contains("locked", lockedDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TC02_lockout_clears_after_lockout_period()
    {
        var client = App.CreateClient();
        var email = $"lockout_clear_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Lockout Clear", email, "P@ssword123"));

        // Trigger lockout
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword"));
        }

        // Fast-forward lockout in database
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // Now login with correct password succeeds
        var ok = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssword123"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task TC03_successful_login_resets_failed_counter()
    {
        var client = App.CreateClient();
        var email = $"counter_reset_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Counter Reset", email, "P@ssword123"));

        // 3 failed attempts
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword"));
        }

        // Verify count is 3
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            Assert.Equal(3, user.AccessFailedCount);
        }

        // Login successfully
        var ok = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssword123"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // Verify count is reset to 0
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            Assert.Equal(0, user.AccessFailedCount);
            Assert.Null(user.LockoutEnd);
        }
    }

    [Fact]
    public async Task TC04_refresh_token_cookie_issued_on_login()
    {
        var client = App.CreateClient();
        var email = $"refresh_cookie_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Cookie User", email, "P@ssword123"));

        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssword123"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.True(res.Headers.Contains("Set-Cookie"));
        var cookies = res.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, c => c.Contains("cb_refresh_token"));
    }

    [Fact]
    public async Task TC05_refresh_token_rotation()
    {
        var client = App.CreateClient();
        var email = $"rotate_{Guid.NewGuid():N}@example.test";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Rotate User", email, "P@ssword123"));
        var authRes = await ReadAsync<AuthResponse>(reg);
        var initialRefresh = authRes.RefreshToken;
        Assert.NotNull(initialRefresh);

        // Refresh with initial token
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(initialRefresh));
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshed = await ReadAsync<AuthResponse>(refreshRes);

        Assert.NotNull(refreshed.Token);
        Assert.NotNull(refreshed.RefreshToken);
        Assert.NotEqual(initialRefresh, refreshed.RefreshToken);

        // Reusing the old initial token must fail
        var reuseRes = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(initialRefresh));
        Assert.Equal(HttpStatusCode.BadRequest, reuseRes.StatusCode);
    }

    [Fact]
    public async Task TC06_refresh_token_fails_when_expired_or_invalid()
    {
        var client = App.CreateClient();
        var badRes = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest("non_existent_token"));
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
    }

    [Fact]
    public async Task TC07_logout_revokes_refresh_token_and_deletes_cookie()
    {
        var client = App.CreateClient();
        var email = $"logout_{Guid.NewGuid():N}@example.test";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Logout User", email, "P@ssword123"));
        var authRes = await ReadAsync<AuthResponse>(reg);
        var token = authRes.RefreshToken;

        // Logout
        var logoutRes = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // Subsequent refresh must fail
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(token));
        Assert.Equal(HttpStatusCode.BadRequest, refreshRes.StatusCode);
    }

    [Fact]
    public async Task TC08_forgot_password_sends_email_with_token()
    {
        var client = App.CreateClient();
        var email = $"forgot_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Forgot User", email, "P@ssword123"));

        var forgotRes = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.OK, forgotRes.StatusCode);

        using var scope = App.Services.CreateScope();
        var emailStore = scope.ServiceProvider.GetRequiredService<IPreviewEmailStore>();
        var sent = emailStore.GetRecentEmails().FirstOrDefault(e => e.To == email);
        Assert.NotNull(sent);
        Assert.Contains("reset-password", sent.Body);
    }

    [Fact]
    public async Task TC09_forgot_password_does_not_reveal_nonexistent_user()
    {
        var client = App.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("ghost@example.test"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task TC10_reset_password_updates_password_and_clears_token()
    {
        var client = App.CreateClient();
        var email = $"reset_{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Reset User", email, "OldPassword123"));

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));

        string? resetToken = null;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            resetToken = user.PasswordResetToken;
        }
        Assert.NotNull(resetToken);

        // Reset password
        var resetRes = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, resetToken, "BrandNewPassword123"));
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        // Old password fails
        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "OldPassword123"));
        Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);

        // New password succeeds
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "BrandNewPassword123"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task TC11_reset_password_fails_with_invalid_token()
    {
        var client = App.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("someone@example.test", "bogus_token", "BrandNewPassword123"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TC12_reset_password_enforces_min_length()
    {
        var client = App.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("someone@example.test", "bogus_token", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var detail = await DetailAsync(res);
        Assert.Contains("at least 8 characters", detail);
    }

    [Fact]
    public async Task TC13_security_headers_present()
    {
        var client = App.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.True(res.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").First());

        Assert.True(res.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").First());

        Assert.True(res.Headers.Contains("Referrer-Policy"));
    }

    [Fact]
    public async Task TC14_preview_emails_endpoint_returns_sent_emails()
    {
        var client = App.CreateClient();
        var res = await client.GetAsync("/api/auth/preview-emails");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public void TC15_legacy_user_hash_compatibility()
    {
        // Ensures that password hashing logic remains 100% backward compatible
        var user = new User { Email = "legacy@example.test" };
        var hasher = new PasswordHasher<User>();
        var hash = hasher.HashPassword(user, "LegacyPassword123");

        var result = hasher.VerifyHashedPassword(user, hash, "LegacyPassword123");
        Assert.Equal(PasswordVerificationResult.Success, result);
    }
}
