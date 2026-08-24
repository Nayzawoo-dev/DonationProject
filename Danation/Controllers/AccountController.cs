using Donation.Services;
using Donation.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Donation.Services.LoginServices;

namespace Donation.Controllers;

public class AccountController : Controller
{
    private readonly UserService _userService;
    private readonly LoginService _loginService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(UserService userService, LoginService loginService, ILogger<AccountController> logger)
    {
        _userService = userService;
        _loginService = loginService;
        _logger = logger;
    }

    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var (success, error) = await _userService.RegisterAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            TempData["RegistrationEmail"] = model.Email;
            TempData["SuccessMessage"] = "Registration successful! Please check your email for the OTP verification code.";
            return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Registration failed. Please check your details and try again.");
            return View(model);
        }
    }

    // GET: /Account/VerifyOtp
    [HttpGet]
    public IActionResult VerifyOtp(string? email)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Register));

        var model = new VerifyOtpViewModel { Email = email };
        return View(model);
    }

    // POST: /Account/VerifyOtp
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (success, error) = await _userService.VerifyOtpAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = "Email verified successfully! You can now log in.";
        return RedirectToAction(nameof(Login));
    }

    // POST: /Account/ResendOtp
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Json(new { success = false, message = "Email is required." });

        try
        {
            var (success, error) = await _userService.ResendOtpAsync(email);
            return Json(new { success, message = success ? "A new OTP has been sent to your email." : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResendOtp failed for {Email}", email);
            return Json(new { success = false, message = "Failed to send OTP. Please try again." });
        }
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        if (!ModelState.IsValid) return View(model);

        var (success, error) = await _loginService.LoginAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Role-based redirect
        if (User.IsInRole("ADMIN"))
            return RedirectToAction("Dashboard", "Admin");

        return RedirectToAction("Index", "Home");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _loginService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
