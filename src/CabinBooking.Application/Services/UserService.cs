using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class UserService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit)
{
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
