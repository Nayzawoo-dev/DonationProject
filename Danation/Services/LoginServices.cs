using DatabaseClass.Models;
using Donation.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Donation.Services;

public class LoginServices
{
    public class LoginService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PasswordHasher<User> _userPasswordHasher;

        public LoginService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userPasswordHasher = new PasswordHasher<User>();
        }

        public async Task<(bool Success, string ErrorMessage, string? Role)> LoginAsync(LoginViewModel model)
        {
            if (model == null) return (false, "Invalid login request.", null);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (user == null)
                return (false, "Invalid email or password.", null);

            if (!user.IsActive)
                return (false, "Your account has been deactivated. Please contact an administrator.", null);

            if (!user.EmailVerified)
                return (false, "Your email is not verified. Please verify your OTP to activate your account.", null);

            var verifyResult = _userPasswordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                return (false, "Invalid email or password.", null);
            }

            await SignInUserAsync(
                user.Id.ToString(),
                user.FullName,
                user.Email,
                user.Role,
                model.RememberMe,
                user.ProfileImage ?? string.Empty);

            return (true, string.Empty, user.Role);
        }

        public async Task SignInUserAsync(string userId, string fullName, string email, string role, bool rememberMe, string profileImage)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("RememberMe", rememberMe.ToString()),
                new Claim("ProfileImage", profileImage ?? string.Empty)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(5)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
            }
        }

        public async Task LogoutAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
