using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Task_4.Data;
using Task_4.Models;

namespace Task_4.Middleware;

/// <summary>
/// Middleware to check if authenticated user is blocked and update last activity timestamp.
/// Runs on every request for authenticated users.
/// </summary>
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
        // Only check for authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                {
                    // Get user from database
                    var user = await dbContext.Users.FindAsync(userId);
                    
                    if (user != null)
                    {
                        // Check if user is blocked
                        if (user.IsBlocked)
                        {
                            _logger.LogWarning("Blocked user {UserId} ({Email}) attempted to access the application", 
                                user.Id, user.Email);
                            
                            // Sign out the user
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            
                            // Redirect to login with message
                            context.Response.Redirect("/Auth/Login?blocked=true");
                            return;
                        }

                        // Update last activity timestamp
                        user.LastActivityAt = DateTime.UtcNow;
                        
                        try
                        {
                            await dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, 
                                "Failed to update LastActivityAt for user {UserId}", userId);
                            // Continue even if update fails - activity tracking is not critical
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthenticationCheckMiddleware");
                // Continue on error - don't break the request pipeline
            }
        }

        await _next(context);
    }
}
