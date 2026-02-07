﻿using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Task_4.Data;
using Task_4.Extensions;
using Task_4.Models;
using Task_4.Models.ViewModels;

namespace Task_4.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Hash password using BCrypt
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

                // Create new user
                var user = new User
                {
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow,
                    Status = Status.Unverified,
                    IsBlocked = false,
                    LastActivityAt = DateTime.UtcNow,
                    LastLoggedInAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData.SetSuccessMessage("Registration successful! Please login to continue.");
                return RedirectToAction("Login");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during user registration for email: {Email}", model.Email);
                
                // Detect specific constraint violations
                string errorMessage = "Unable to complete registration. Please try again.";
                
                if (ex.InnerException?.Message.Contains("Email") == true || 
                    ex.InnerException?.Message.Contains("UNIQUE constraint") == true)
                {
                    errorMessage = "This email is already registered. Please use a different email or try logging in.";
                    ModelState.AddModelError(nameof(model.Email), errorMessage);
                }
                else
                {
                    // Log the full error for debugging
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
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Find user by email
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password");
                    return View(model);
                }

                // Check if user is blocked
                if (user.IsBlocked)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been blocked");
                    return View(model);
                }

                // Update last login time (non-critical - continue even if it fails)
                try
                {
                    user.LastLoggedInAt = DateTime.UtcNow;
                    user.LastActivityAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update last login time for user: {UserId}", user.Id);
                    // Continue with login even if timestamp update fails
                }

                // Create authentication claims
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
}