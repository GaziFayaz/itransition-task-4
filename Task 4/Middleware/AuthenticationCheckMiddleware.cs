using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Task_4.Data;
using Task_4.Models;

namespace Task_4.Middleware;

public class AuthenticationCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationCheckMiddleware> _logger;

    public AuthenticationCheckMiddleware(RequestDelegate next, ILogger<AuthenticationCheckMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                {
                    var user = await dbContext.Users.FindAsync(userId);

                    if (user != null)
                    {
                        if (user.Status == Status.Blocked)
                        {
                            _logger.LogWarning("Blocked user {UserId} ({Email}) attempted to access the application",
                                user.Id, user.Email);

                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                            context.Response.Redirect("/Auth/Login?blocked=true");
                            return;
                        }

                        user.LastActivityAt = DateTime.UtcNow;

                        try
                        {
                            await dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Failed to update LastActivityAt for user {UserId}", userId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthenticationCheckMiddleware");
            }
        }

        await _next(context);
    }
}