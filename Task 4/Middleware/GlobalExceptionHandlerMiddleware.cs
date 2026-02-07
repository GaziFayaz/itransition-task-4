using System.Net;

namespace Task_4.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Store error message in TempData for toast notification
        var tempDataProvider = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
        var tempData = tempDataProvider.GetTempData(context);
        
        tempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
        
        // Redirect to referrer or home page
        var referer = context.Request.Headers["Referer"].ToString();
        context.Response.Redirect(string.IsNullOrEmpty(referer) ? "/" : referer);
        
        return Task.CompletedTask;
    }
}
