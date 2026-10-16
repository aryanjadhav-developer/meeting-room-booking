using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CabinBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Api.Auth;

/// <summary>
/// A JWT stays valid for hours, so on every request we re-check the account in the database:
/// deactivated (or deleted) users are rejected immediately, and the role claim is replaced with
/// the current role so promotions/demotions apply without signing in again.
/// </summary>
public static class AccountStatusCheck
{
    private const string RejectionKey = "auth.rejection";

    public static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var sub = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(sub, out var userId))
        {
            Reject(ctx, "Invalid session. Please sign in again.");
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role })
            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

        if (user is null)
        {
            Reject(ctx, "Your account no longer exists. Please contact an administrator.");
            return;
        }
        if (!user.IsActive)
        {
            Reject(ctx, "Your account has been deactivated. Please contact an administrator.");
            return;
        }

        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
        foreach (var stale in identity.FindAll("role").ToList()) identity.RemoveClaim(stale);
        identity.AddClaim(new Claim("role", user.Role.ToString()));
    }

    /// <summary>Returns the rejection reason as ProblemDetails so the UI can tell the user why they were signed out.</summary>
    public static async Task OnChallenge(JwtBearerChallengeContext ctx)
    {
        if (ctx.HttpContext.Items[RejectionKey] is not string reason) return;

        ctx.HandleResponse();
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Signed out", Detail = reason },
            options: null, contentType: "application/problem+json");
    }

    private static void Reject(TokenValidatedContext ctx, string reason)
    {
        ctx.HttpContext.Items[RejectionKey] = reason;
        ctx.Fail(reason);
    }
}
