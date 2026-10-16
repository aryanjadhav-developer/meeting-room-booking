# ADR 0005: Refresh Token Rotation, Secure Cookies, and Account Lockout

## Status
Accepted

## Context
Short-lived JWT access tokens limit the window of opportunity if an access token is compromised. However, asking users to re-authenticate every 15–60 minutes degrades user experience. Storing refresh tokens in JavaScript-accessible browser storage (`localStorage`) exposes them to Cross-Site Scripting (XSS) attacks. Furthermore, brute-force credential stuffing requires server-side mitigation.

## Decision
We implemented a multi-layered security strategy:
1. **Refresh Token Rotation:** Refresh tokens are single-use cryptographically secure random 256-bit strings (`RandomNumberGenerator`). Upon each `/api/auth/refresh` request, the previous refresh token is immediately invalidated and replaced with a newly generated one.
2. **HttpOnly Secure Cookies:** Refresh tokens are transported primarily via `HttpOnly`, `SameSite=Lax`, `Secure` cookies (`cb_refresh_token`), preventing client-side JavaScript access and XSS theft. A fallback JSON body mechanism is supported for non-browser HTTP clients.
3. **Account Lockout:** After 5 consecutive failed login attempts within an account, the user is locked out for 15 minutes (`LockoutEnd = clock.GetUtcNow().AddMinutes(15)`).
4. **Rate Limiting:** Built-in fixed-window rate limiter limits authentication endpoints (`/api/auth/*`) to prevent automated credential enumeration.

## Consequences
### Positive
- Robust defense against XSS token harvesting and credential brute-force attacks.
- Seamless user session renewal without interrupting workflows.
- Detects token replay attacks: attempting to reuse an already-rotated refresh token signals potential theft.

### Negative
- Requires maintaining refresh token hash and expiry state in the user database table.
