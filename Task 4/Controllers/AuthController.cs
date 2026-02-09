using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Task_4.Data;
using Task_4.Extensions;
using Task_4.Models;
using Task_4.Models.ViewModels;
using Task_4.Services;

namespace Task_4.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, ILogger<AuthController> logger, IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var verificationToken = GenerateSecureToken();
                var tokenExpiry = DateTime.UtcNow.AddHours(24);

                var user = new User
                {
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow,
                    Status = Status.Unverified,
                    LastActivityAt = DateTime.UtcNow,
                    LastLoggedInAt = DateTime.UtcNow,
                    EmailVerificationToken = verificationToken,
                    EmailVerificationTokenExpiry = tokenExpiry
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                try
                {
                    var verificationLink = Url.Action(
                        "VerifyEmail",
                        "Auth",
                        new { token = verificationToken, email = model.Email },
                        Request.Scheme
                    );

                    await _emailService.SendVerificationEmailAsync(user.Email, user.Name, verificationLink!);
                    TempData.SetSuccessMessage(
                        "Registration successful! Please check your email to verify your account.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send verification email to {Email}", model.Email);
                    TempData.SetWarningMessage(
                        "Registration successful, but we couldn't send the verification email. Please contact support.");
                }

                return RedirectToAction("Login");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during user registration for email: {Email}", model.Email);

                string errorMessage = "Unable to complete registration. Please try again.";

                if (ex.InnerException?.Message.Contains("Email") == true ||
                    ex.InnerException?.Message.Contains("UNIQUE constraint") == true)
                {
                    errorMessage = "This email is already registered. Please use a different email or try logging in.";
                    ModelState.AddModelError(nameof(model.Email), errorMessage);
                }
                else
                {
                    _logger.LogError("DbUpdateException details - InnerException: {InnerException}",
                        ex.InnerException?.Message);
                }

                TempData.SetErrorMessage(errorMessage);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user registration for email: {Email}", model.Email);
                TempData.SetErrorMessage("An unexpected error occurred. Please try again.");
                return View(model);
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(bool blocked = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (blocked)
        {
            TempData.SetErrorMessage("Your account has been blocked.");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password");
                    return View(model);
                }

                if (user.Status == Status.Blocked)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been blocked");
                    return View(model);
                }

                try
                {
                    user.LastLoggedInAt = DateTime.UtcNow;
                    user.LastActivityAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update last login time for user: {UserId}", user.Id);
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("Status", user.Status.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                TempData.SetSuccessMessage($"Welcome back, {user.Name}!");
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for email: {Email}", model.Email);
                TempData.SetErrorMessage("An error occurred during login. Please try again.");
                return View(model);
            }
        }

        return View(model);
    }

    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData.SetInfoMessage("You have been logged out successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            TempData.SetErrorMessage("An error occurred during logout.");
        }

        return RedirectToAction("Login");
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string token, string email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            TempData.SetErrorMessage("Invalid verification link.");
            return RedirectToAction("Login");
        }

        try
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                TempData.SetErrorMessage("User not found.");
                return RedirectToAction("Login");
            }

            if (user.Status == Status.Verified)
            {
                TempData.SetInfoMessage("Your email is already verified. You can log in.");
                return RedirectToAction("Login");
            }

            if (user.EmailVerificationToken != token)
            {
                TempData.SetErrorMessage("Invalid verification token.");
                return RedirectToAction("Login");
            }

            if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            {
                TempData.SetErrorMessage("Verification link has expired. Please request a new verification email.");
                return RedirectToAction("ResendVerification", new { email = email });
            }

            user.Status = Status.Verified;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            await _context.SaveChangesAsync();

            TempData.SetSuccessMessage("Email verified successfully! You can now log in.");
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification for {Email}", email);
            TempData.SetErrorMessage("An error occurred during verification. Please try again.");
            return RedirectToAction("Login");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerification()
    {
        try
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _context.Users.FindAsync(currentUserId);

            if (user == null)
            {
                TempData.SetErrorMessage("User not found.");
                return RedirectToAction("Index", "Home");
            }

            if (user.Status == Status.Verified)
            {
                TempData.SetInfoMessage("Your email is already verified.");
                return RedirectToAction("Index", "Home");
            }

            if (user.Status == Status.Blocked)
            {
                TempData.SetErrorMessage("Your account has been blocked. Please contact support.");
                return RedirectToAction("Index", "Home");
            }

            var verificationToken = GenerateSecureToken();
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            user.EmailVerificationToken = verificationToken;
            user.EmailVerificationTokenExpiry = tokenExpiry;
            await _context.SaveChangesAsync();

            var verificationLink = Url.Action(
                "VerifyEmail",
                "Auth",
                new { token = verificationToken, email = user.Email },
                Request.Scheme
            );

            await _emailService.SendVerificationEmailAsync(user.Email, user.Name, verificationLink!);

            TempData.SetSuccessMessage("Verification email sent! Please check your inbox.");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email");
            TempData.SetErrorMessage("An error occurred while sending the verification email. Please try again.");
            return RedirectToAction("Index", "Home");
        }
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}