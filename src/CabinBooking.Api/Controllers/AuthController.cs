using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create an account. The first account ever created becomes an Admin.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpGet("me")]
    [Authorize]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetUserAsync(currentUser.UserId, ct);
}
