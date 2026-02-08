using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Task_4.Data;
using Task_4.Extensions;
using Task_4.Models;

namespace Task_4.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.LastLoggedInAt)
                .ToListAsync();
            return View(users);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyMe()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var currentUser = await _context.Users.FindAsync(currentUserId);

                if (currentUser == null)
                {
                    TempData.SetErrorMessage("User not found");
                    return RedirectToAction(nameof(Index));
                }

                if (currentUser.Status == Status.Verified)
                {
                    TempData.SetInfoMessage("You are already verified");
                    return RedirectToAction(nameof(Index));
                }

                if (currentUser.Status == Status.Blocked)
                {
                    TempData.SetErrorMessage("Cannot verify a blocked account");
                    return RedirectToAction(nameof(Index));
                }

                currentUser.Status = Status.Verified;
                await _context.SaveChangesAsync();

                TempData.SetSuccessMessage("Your account has been verified successfully!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData.SetErrorMessage("An error occurred while verifying your account");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUsers(List<int>? userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                TempData.SetErrorMessage("No users selected");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id) && u.Status != Status.Blocked)
                    .ToListAsync();

                if (users.Count == 0)
                {
                    TempData.SetWarningMessage("No users were blocked (already blocked or not found)");
                    return RedirectToAction(nameof(Index));
                }

                foreach (var user in users)
                {
                    user.Status = Status.Blocked;
                }

                await _context.SaveChangesAsync();

                // Check if current user blocked themselves
                if (userIds.Contains(currentUserId))
                {
                    TempData.SetSuccessMessage($"{users.Count} user(s) blocked. You blocked yourself and will be logged out.");
                    return RedirectToAction("Logout", "Auth");
                }

                TempData.SetSuccessMessage($"{users.Count} user(s) blocked successfully");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData.SetErrorMessage("An error occurred while blocking users");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockUsers(List<int> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                TempData.SetErrorMessage("No users selected");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id) && u.Status == Status.Blocked)
                    .ToListAsync();

                if (users.Count == 0)
                {
                    TempData.SetWarningMessage("No users were unblocked (not blocked or not found)");
                    return RedirectToAction(nameof(Index));
                }

                foreach (var user in users)
                {
                    user.Status = Status.Verified;
                }

                await _context.SaveChangesAsync();

                TempData.SetSuccessMessage($"{users.Count} user(s) unblocked successfully");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData.SetErrorMessage("An error occurred while unblocking users");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUsers(List<int> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                TempData.SetErrorMessage("No users selected");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToListAsync();

                if (users.Count == 0)
                {
                    TempData.SetWarningMessage("No users found to delete");
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.RemoveRange(users);
                await _context.SaveChangesAsync();

                // Check if current user deleted themselves
                if (userIds.Contains(currentUserId))
                {
                    TempData.SetSuccessMessage($"{users.Count} user(s) deleted. You deleted yourself and will be logged out.");
                    return RedirectToAction("Logout", "Auth");
                }

                TempData.SetSuccessMessage($"{users.Count} user(s) deleted successfully");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData.SetErrorMessage("An error occurred while deleting users");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
