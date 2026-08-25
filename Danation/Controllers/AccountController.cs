using Donation.Services;
using Donation.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using static Donation.Services.LoginServices;

namespace Donation.Controllers;

[EnableRateLimiting("RoleBasedPolicy")]
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
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (!ModelState.IsValid)
        {
            if (isAjax)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = errors.FirstOrDefault() ?? "Invalid registration details." });
            }
            return View(model);
        }

        try
        {
            var (success, error) = await _userService.RegisterAsync(model);
            if (!success)
            {
                if (isAjax)
                    return Json(new { success = false, message = error });

                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            TempData["RegistrationEmail"] = model.Email;
            TempData["SuccessMessage"] = "Registration successful! Please check your email for the OTP verification code.";

            if (isAjax)
            {
                return Json(new
                {
                    success = true,
                    message = "Registration successful! Redirecting to email verification...",
                    redirectUrl = Url.Action(nameof(VerifyOtp), new { email = model.Email })
                });
            }

            return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}", model.Email);
            var err = "Registration failed. Please check your details and try again.";
            if (isAjax)
                return Json(new { success = false, message = err });

            ModelState.AddModelError(string.Empty, err);
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
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (!ModelState.IsValid)
        {
            if (isAjax)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = errors.FirstOrDefault() ?? "Invalid OTP code." });
            }
            return View(model);
        }

        var (success, error) = await _userService.VerifyOtpAsync(model);
        if (!success)
        {
            if (isAjax)
                return Json(new { success = false, message = error });

            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        TempData["SuccessMessage"] = "Email verified successfully! You can now log in.";

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "Email verified successfully! Redirecting to login...",
                redirectUrl = Url.Action(nameof(Login))
            });
        }

        return RedirectToAction(nameof(Login));
    }

    // POST: /Account/ResendOtp (AJAX)
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
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (!ModelState.IsValid)
        {
            if (isAjax)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = errors.FirstOrDefault() ?? "Please enter your email and password." });
            }
            return View(model);
        }

        var (success, error, role) = await _loginService.LoginAsync(model);
        if (!success)
        {
            if (isAjax)
                return Json(new { success = false, message = error });

            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        string targetUrl = Url.Action("Index", "Home")!;
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            targetUrl = returnUrl;
        }
        else if (role == "ADMIN")
        {
            targetUrl = Url.Action("Dashboard", "Admin")!;
        }

        if (isAjax)
        {
            return Json(new
            {
                success = true,
                message = "Login successful! Redirecting...",
                redirectUrl = targetUrl
            });
        }

        return Redirect(targetUrl);
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

    // =============================================
    //  Forgot Password — Multi-step AJAX Flow
    // =============================================

    // GET: /Account/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View();
    }

    /// <summary>
    /// POST: /Account/SendResetOtp (AJAX, Step 1)
    /// Sends a password reset OTP to the email.
    /// Always returns generic message — never reveals if email exists (anti-enumeration).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendResetOtp([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Json(new { success = false, message = "Please enter a valid email address." });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, message) = await _userService.ForgotPasswordAsync(email.Trim().ToLower(), clientIp);

        // Always return success = true with generic message (anti-enumeration)
        return Json(new { success = true, message });
    }

    /// <summary>
    /// POST: /Account/VerifyResetOtp (AJAX, Step 2)
    /// Verifies the reset OTP. On success, returns a reset token for Step 3.
    /// The token is short-lived (10 min) and stored server-side in IMemoryCache.
    /// The token is NOT a user credential — it only authorizes a single password reset.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyResetOtp([FromForm] string email, [FromForm] string otpCode)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
            return Json(new { success = false, message = "Email and code are required." });

        var (success, message, resetToken) = _userService.VerifyPasswordResetOtp(email.Trim().ToLower(), otpCode.Trim());

        if (!success)
            return Json(new { success = false, message });

        // Return token to client so it can be submitted with the password reset
        // This is safe: the token has no privilege itself, it only allows ONE password reset
        // and expires in 10 minutes in IMemoryCache
        return Json(new { success = true, message, resetToken });
    }

    /// <summary>
    /// POST: /Account/ResetPassword (AJAX, Step 3)
    /// Resets the password. Requires valid email + reset token from Step 2.
    /// Redirects to Login on success.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        var (success, message) = await _userService.ResetPasswordAsync(
            model.Email.Trim().ToLower(),
            model.ResetToken,
            model.NewPassword);

        if (!success)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = "Password reset successfully!",
            redirectUrl = Url.Action(nameof(Login))
        });
    }
}
