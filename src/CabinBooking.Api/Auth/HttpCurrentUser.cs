using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Enums;

namespace CabinBooking.Api.Auth;

/// <summary>Reads the signed-in user from the JWT. Outside a request (background jobs) it is anonymous.</summary>
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;
}
