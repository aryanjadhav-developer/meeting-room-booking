using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Services;
using CabinBooking.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CabinBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    private const string RefreshCookieName = "cb_refresh_token";

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = await auth.RegisterAsync(request, ct);
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            SetRefreshTokenCookie(response.RefreshToken);
        }
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await auth.LoginAsync(request, ct);
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            SetRefreshTokenCookie(response.RefreshToken);
        }
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest? body, CancellationToken ct)
    {
        var token = !string.IsNullOrWhiteSpace(body?.RefreshToken)
            ? body.RefreshToken
            : Request.Cookies[RefreshCookieName];
        var response = await auth.RefreshTokenAsync(token, ct);
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            SetRefreshTokenCookie(response.RefreshToken);
        }
        return Ok(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? body, CancellationToken ct)
    {
        var token = !string.IsNullOrWhiteSpace(body?.RefreshToken)
            ? body.RefreshToken
            : Request.Cookies[RefreshCookieName];
        await auth.RevokeRefreshTokenAsync(token, ct);
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin))
        {
            origin = $"{Request.Scheme}://{Request.Host}";
        }
        await auth.ForgotPasswordAsync(request, origin, ct);
        return Ok(new { message = "If your e-mail is registered, you will receive a password reset link shortly." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return Ok(new { message = "Your password has been successfully reset. You can now log in." });
    }

    [HttpGet("preview-emails")]
    [Authorize(Roles = "Admin")]
    public ActionResult<IReadOnlyList<SentEmail>> GetPreviewEmails([FromServices] IPreviewEmailStore store)
    {
        return Ok(store.GetRecentEmails());
    }

    [HttpDelete("preview-emails")]
    [Authorize(Roles = "Admin")]
    public IActionResult ClearPreviewEmails([FromServices] IPreviewEmailStore store)
    {
        store.Clear();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetUserAsync(currentUser.UserId, ct);

    [HttpPut("me")]
    [Authorize]
    public Task<UserDto> UpdateMe([FromBody] UpdateProfileRequest request, [FromServices] UserService users, CancellationToken ct) =>
        users.UpdateProfileAsync(currentUser.UserId, request, ct);

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
