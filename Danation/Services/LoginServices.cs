using DatabaseClass.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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

        public async Task<(bool Success, string ErrorMessage)> LoginAsync(LoginViewModel model)
        {
            if (model == null) return (false, "Invalid login request.");

            // 2. Check User
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (user != null)
            {
                if (!user.IsActive)
                {
                    return (false, "Your account has been deactivated. Please contact an administrator.");
                }

                if (!user.IsEmailVerified)
                {
                    return (false, "Your email is not verified. Please verify your OTP to activate your account.");
                }

                var verifyResult = _userPasswordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                if (verifyResult == PasswordVerificationResult.Failed)
                {
                    // Fallback check for plain text legacy password
                    if (user.PasswordHash == model.Password)
                    {
                        user.PasswordHash = _userPasswordHasher.HashPassword(user, model.Password);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        return (false, "Invalid email or password.");
                    }
                }

                await SignInUserAsync(user.UserId.ToString(), user.FullName, user.Email, user.Role, model.RememberMe, user.ProfileImageUrl);
                return (true, string.Empty);
            }

            return (false, "Invalid email or password.");
        }

        public async Task SignInUserAsync(string userId, string fullName, string email, string role, bool rememberMe, string ProfileImage)
        {
            var claims = new List<Claim>
            {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Name, fullName),
        new Claim(ClaimTypes.Email, email),
        new Claim(ClaimTypes.Role, role),
        new Claim("RememberMe", rememberMe.ToString()),
        new Claim("ProfileImage", ProfileImage)
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
