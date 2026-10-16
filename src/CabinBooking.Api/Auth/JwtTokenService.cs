using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CabinBooking.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CabinBooking";
    public string Audience { get; set; } = "CabinBooking";
    /// <summary>At least 32 characters. Keep it out of source control in production.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 12;

    public SymmetricSecurityKey GetKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddHours(o.ExpiryHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            // Short "role" name: JwtSecurityToken writes claim types verbatim and the API validates with RoleClaimType = "role".
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            o.Issuer, o.Audience, claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(o.GetKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
