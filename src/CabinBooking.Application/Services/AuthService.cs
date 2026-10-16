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
    TimeProvider clock)
{
    private const int MinPasswordLength = 8;

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

        var user = new User
        {
            Name = name,
            Email = email,
            Role = UserRole.Employee,
            CreatedAt = clock.GetUtcNow()
        };
        user.PasswordHash = passwords.Hash(user, request.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !passwords.Verify(user, request.Password ?? ""))
            throw new AppValidationException("Invalid e-mail or password.");
        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated. Please contact an administrator.");

        return Issue(user);
    }

    public async Task<UserDto> GetUserAsync(Guid id, CancellationToken ct) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User not found.")).ToDto();

    private AuthResponse Issue(User user)
    {
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email) =>
        email.Length <= 200 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
}
