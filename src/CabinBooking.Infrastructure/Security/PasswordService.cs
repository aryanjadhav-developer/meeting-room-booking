using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CabinBooking.Infrastructure.Security;

/// <summary>PBKDF2 hashing via ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.</summary>
public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
