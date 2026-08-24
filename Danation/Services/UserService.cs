using DatabaseClass.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using static Donation.Services.LoginServices;

namespace Donation.Services;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly LoginService _loginService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(AppDbContext context, EmailService emailService, IMemoryCache cache, IWebHostEnvironment env, LoginService loginService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _emailService = emailService;
        _cache = cache;
        _env = env;
        _passwordHasher = new PasswordHasher<User>();
        _loginService = loginService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterViewModel model)
    {
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
        if (existingUser != null)
        {
            if (existingUser.IsEmailVerified)
            {
                return (false, "An account with this email address already exists.");
            }
            else
            {
                // User started registration earlier but didn't verify OTP. Update password & resend OTP.
                existingUser.FullName = model.FullName;
                existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, model.Password);
                existingUser.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await GenerateAndSendOtpAsync(existingUser.Email, existingUser.FullName);
                return (true, string.Empty);
            }
        }

        var newUser = new User
        {
            FullName = model.FullName,
            Email = model.Email.Trim(),
            IsEmailVerified = false,
            IsActive = false, // Deactivated until OTP verification
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        await GenerateAndSendOtpAsync(newUser.Email, newUser.FullName);
        return (true, string.Empty);
    }

    public async Task GenerateAndSendOtpAsync(string email, string fullName)
    {
        string otpCode = Random.Shared.Next(100000, 999999).ToString();
        string cacheKey = $"OTP_{email.ToLower()}";

        _cache.Set(cacheKey, otpCode, TimeSpan.FromMinutes(10));

        string subject = "Your Verification OTP Code - WebChatBot";
        string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2>Hello {fullName},</h2>
                    <p>Thank you for registering with WebChatBot. Please use the following 6-digit OTP code to verify your account:</p>
                    <div style='background: #f4f4f5; font-size: 28px; font-weight: bold; letter-spacing: 5px; padding: 15px; text-align: center; border-radius: 8px; margin: 20px 0;'>
                        {otpCode}
                    </div>
                    <p>This code will expire in 10 minutes.</p>
                    <p>If you did not request this, please ignore this email.</p>
                </div>";

        await _emailService.SendEmailAsync(email, subject, body);
    }

    public async Task<(bool Success, string ErrorMessage)> VerifyOtpAsync(VerifyOtpViewModel model)
    {
        string cacheKey = $"OTP_{model.Email.ToLower()}";
        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || cachedOtp != model.OtpCode)
        {
            return (false, "Invalid or expired OTP code. Please request a new one.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
        if (user == null)
        {
            return (false, "User account not found.");
        }

        user.IsEmailVerified = true;
        user.IsActive = true; // Activate account after successful verification
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _cache.Remove(cacheKey);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ResendOtpAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user == null)
        {
            return (false, "User account not found.");
        }

        if (user.IsEmailVerified && user.IsActive)
        {
            return (false, "Account is already verified and active.");
        }

        await GenerateAndSendOtpAsync(user.Email, user.FullName);
        return (true, string.Empty);
    }

    public async Task<UserProfileViewModel?> GetUserProfileAsync(int userId, string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.Email == email);
        if (user == null && admin == null) return null;

        if (user != null)
        {
            return new UserProfileViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                ProfileImageUrl = user.ProfileImageUrl,
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt
            };
        }

        return null;
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateProfileAsync(EditProfileViewModel model, int currentUserId)
    {
        if (model.UserId != currentUserId)
        {
            return (false, "Unauthorized profile modification attempt.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId && u.Email == model.Email);
        if (user == null && admin == null)
        {
            return (false, "Profile not found.");
        }

        // Handle Profile Image Upload
        if (model.NewProfileImage != null && model.NewProfileImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(model.NewProfileImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                return (false, "Invalid image format. Only JPG, PNG, WEBP, and GIF files are allowed.");
            }

            if (model.NewProfileImage.Length > 2 * 1024 * 1024)
            {
                return (false, "Profile image file size must be less than 2MB.");
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = $"profile_{currentUserId}_{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.NewProfileImage.CopyToAsync(stream);
            }

            // Delete old custom profile image if exists
            if (user != null && !string.IsNullOrEmpty(user.ProfileImageUrl) && user.ProfileImageUrl.StartsWith("/uploads/profiles/"))
            {
                string oldFilePath = Path.Combine(_env.WebRootPath, user.ProfileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { }
                }

                user.ProfileImageUrl = $"/uploads/profiles/{uniqueFileName}";

            }
        }


        user.FullName = model.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            var rememberMeValue = httpContext.User
                .FindFirst("RememberMe")?.Value;

            bool rememberMe = bool.TryParse(
                rememberMeValue,
                out var result
            ) && result;

            var role = httpContext.User
                .FindFirst(ClaimTypes.Role)?.Value;

            if (user != null)
            {
                await _loginService.SignInUserAsync(
                    user.UserId.ToString(),
                    user.FullName,
                    user.Email,
                    user.Role,
                    rememberMe,
                    user.ProfileImageUrl
                );
            }
        }
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordViewModel model)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed && user.PasswordHash != model.CurrentPassword)
        {
            return (false, "Incorrect current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }
}
