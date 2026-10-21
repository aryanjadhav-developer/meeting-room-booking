using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class UserService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, IPasswordService passwords, TimeProvider clock)
{
    public async Task<IReadOnlyList<DirectoryUserDto>> GetDirectoryAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name)
            .Select(u => new DirectoryUserDto(u.Id, u.Name, u.Email)).ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        if (name.Length is < 2 or > 100) throw new AppValidationException("Name must be 2–100 characters.");
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || !email.Contains('@'))
            throw new AppValidationException("Please enter a valid e-mail address.");
        if ((request.Password?.Length ?? 0) < 8) throw new AppValidationException("Password must be at least 8 characters.");
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this e-mail already exists.");

        var user = new Domain.Entities.User { Name = name, Email = email, Role = request.Role, CreatedAt = clock.GetUtcNow(), EmailConfirmed = true };
        user.PasswordHash = passwords.Hash(user, request.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("UserCreated", "User", user.Id.ToString(), $"Created user '{name}' ({email}) as {user.Role}", ct);
        return user.ToDto();
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User not found.");

        if (id == currentUser.UserId && (request.Role != UserRole.Admin || !request.IsActive))
            throw new AppValidationException("You cannot remove your own admin access or deactivate yourself.");

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("UserUpdated", "User", user.Id.ToString(), $"Updated user '{user.Name}' ({user.Email}) to Role={user.Role}, Active={user.IsActive}", ct);
        return user.ToDto();
    }
}
