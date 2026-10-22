using System.Net.Mail;
using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Options;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CabinBooking.Application.Services;

public class AuthService(
    IAppDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    IOptions<AuthOptions> authOptions,
    IEmailSender emailSender,
    TimeProvider clock)
{
    private const int MinPasswordLength = 8;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Registers a user as an Employee. Initial Admin is seeded from configuration.</summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var email = NormalizeEmail(request.Email);

        if (name.Length is < 2 or > 100) throw new AppValidationException("Please enter your name (2–100 characters).");
        if (!IsValidEmail(email)) throw new AppValidationException("Please enter a valid e-mail address.");
        if ((request.Password?.Length ?? 0) < MinPasswordLength)
            throw new AppValidationException($"Password must be at least {MinPasswordLength} characters.");

        var domains = authOptions.Value.AllowedEmailDomains;
        if (domains.Length > 0 && !domains.Any(d => email.EndsWith("@" + d.ToLowerInvariant(), StringComparison.Ordinal)))
            throw new AppValidationException($"Only {string.Join(", ", domains.Select(d => "@" + d))} e-mail addresses can register.");

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this e-mail already exists.");

        var refreshToken = GenerateSecureToken();
        var user = new User
        {
            Name = name,
            Email = email,
            Role = UserRole.Employee,
            CreatedAt = clock.GetUtcNow(),
            EmailConfirmed = true,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = clock.GetUtcNow().AddDays(7)
        };
        user.PasswordHash = passwords.Hash(user, request.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Issue(user, refreshToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is not null && user.LockoutEnd.HasValue && user.LockoutEnd.Value > clock.GetUtcNow())
        {
            var remaining = user.LockoutEnd.Value - clock.GetUtcNow();
            var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
            throw new AppValidationException($"Account is temporarily locked due to multiple failed login attempts. Please try again in {minutes} minute{(minutes == 1 ? "" : "s")}.");
        }

        if (user is null || !passwords.Verify(user, request.Password ?? ""))
        {
            if (user is not null)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = clock.GetUtcNow().Add(LockoutDuration);
                }
                await db.SaveChangesAsync(ct);
            }
            throw new AppValidationException("Invalid e-mail or password.");
        }

        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated. Please contact an administrator.");

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        var refreshToken = GenerateSecureToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = clock.GetUtcNow().AddDays(7);
        await db.SaveChangesAsync(ct);

        return Issue(user, refreshToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AppValidationException("Refresh token is required.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);
        if (user is null || !user.RefreshTokenExpiresAt.HasValue || user.RefreshTokenExpiresAt.Value <= clock.GetUtcNow())
            throw new AppValidationException("Invalid or expired refresh token.");

        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated. Please contact an administrator.");

        var newRefreshToken = GenerateSecureToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAt = clock.GetUtcNow().AddDays(7);
        await db.SaveChangesAsync(ct);

        return Issue(user, newRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);
        if (user is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, string clientBaseUrl, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email)) return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive) return;

        var token = GenerateSecureToken();
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiresAt = clock.GetUtcNow().AddHours(1);
        await db.SaveChangesAsync(ct);

        var resetLink = $"{clientBaseUrl.TrimEnd('/')}/reset-password?token={token}";
        var body = $"""
            <h2>Meeting Room Booking - Password Reset</h2>
            <p>Hello {user.Name},</p>
            <p>A password reset was requested for your account. Click the link below to set a new password (valid for 1 hour):</p>
            <p><a href="{resetLink}">{resetLink}</a></p>
            <p>If you did not request this, please ignore this email.</p>
            """;

        await emailSender.SendEmailAsync(user.Email, "Reset your password - Meeting Room Booking", body, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if ((request.NewPassword?.Length ?? 0) < MinPasswordLength)
            throw new AppValidationException($"Password must be at least {MinPasswordLength} characters.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
            user.PasswordResetToken != request.Token ||
            !user.PasswordResetTokenExpiresAt.HasValue ||
            user.PasswordResetTokenExpiresAt.Value <= clock.GetUtcNow())
        {
            throw new AppValidationException("Invalid or expired password reset token.");
        }

        user.PasswordHash = passwords.Hash(user, request.NewPassword!);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetUserAsync(Guid id, CancellationToken ct) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User not found.")).ToDto();

    private AuthResponse Issue(User user, string? refreshToken = null)
    {
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto(), refreshToken);
    }

    private static string GenerateSecureToken() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email) =>
        email.Length <= 200 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
}
