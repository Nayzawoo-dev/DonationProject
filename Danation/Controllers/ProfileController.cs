using Donation.Services;
using Donation.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Donation.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserService _userService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(UserService userService, ILogger<ProfileController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }

    // GET: /Profile
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var profile = await _userService.GetUserProfileAsync(userId);
        if (profile == null) return NotFound();
        return View(profile);
    }

    // GET: /Profile/Edit
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var userId = GetCurrentUserId();
        var model = await _userService.GetEditProfileViewModelAsync(userId);
        if (model == null) return NotFound();
        return View(model);
    }

    // POST: /Profile/Edit (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        var userId = GetCurrentUserId();
        var (success, error) = await _userService.UpdateProfileAsync(model, userId);

        if (success)
            return Json(new { success = true, message = "Profile updated successfully!" });

        return Json(new { success = false, message = error });
    }

    // POST: /Profile/ChangePassword (AJAX) — role "USER" (uppercase, matching the claim)
    [Authorize(Roles = "USER")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        var userId = GetCurrentUserId();
        var (success, error) = await _userService.ChangePasswordAsync(userId, model);

        return Json(new { success, message = success ? "Password changed successfully!" : error });
    }

    // GET: /Profile/Public/5 — Public user profile (no [Authorize] — accessible to everyone)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Public(int id)
    {
        if (id <= 0) return NotFound();

        var profile = await _userService.GetPublicProfileAsync(id);
        if (profile == null) return NotFound();

        return View(profile);
    }
}
