using DatabaseClass.Models;
using Donation.ViewModels.Account;
using Donation.ViewModels.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
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
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext context,
        EmailService emailService,
        IMemoryCache cache,
        IWebHostEnvironment env,
        LoginService loginService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserService> logger)
    {
        _context = context;
        _emailService = emailService;
        _cache = cache;
        _env = env;
        _passwordHasher = new PasswordHasher<User>();
        _loginService = loginService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterViewModel model)
    {
        // Check for existing email
        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

        if (existingByEmail != null)
        {
            if (existingByEmail.EmailVerified)
                return (false, "An account with this email already exists.");

            // Incomplete registration — update and resend OTP
            existingByEmail.FullName = model.FullName;
            existingByEmail.Username = model.Username;
            existingByEmail.Phone = model.Phone;
            existingByEmail.PasswordHash = _passwordHasher.HashPassword(existingByEmail, model.Password);
            existingByEmail.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await GenerateAndSendOtpAsync(existingByEmail.Email, existingByEmail.FullName);
            return (true, string.Empty);
        }

        // Check for existing username
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
        if (existingByUsername != null)
            return (false, "This username is already taken. Please choose another.");

        var newUser = new User
        {
            FullName = model.FullName,
            Username = model.Username,
            Email = model.Email.Trim(),
            Phone = model.Phone,
            Role = "USER",
            EmailVerified = false,
            IsActive = false,
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
        // Cryptographically secure 6-digit OTP
        string otpCode = GenerateSecureOtp();
        string cacheKey = $"OTP_{email.ToLower()}";

        var otpData = new OtpCacheEntry
        {
            Code = otpCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, otpData, TimeSpan.FromMinutes(5));

        string subject = "Your Verification OTP — Danation Charity Platform";
        string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333;'>
                <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 12px 12px 0 0; text-align: center;'>
                    <h1 style='color: white; margin: 0; font-size: 28px;'>❤️ Danation</h1>
                    <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0;'>Charity Platform</p>
                </div>
                <div style='background: #fff; padding: 30px; border: 1px solid #e2e8f0; border-radius: 0 0 12px 12px;'>
                    <h2 style='color: #2d3748;'>Hello {fullName},</h2>
                    <p style='color: #4a5568;'>Thank you for registering with Danation. Use the following OTP to verify your account:</p>
                    <div style='background: #f7fafc; font-size: 36px; font-weight: bold; letter-spacing: 10px; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0; color: #667eea; border: 2px dashed #667eea;'>
                        {otpCode}
                    </div>
                    <p style='color: #e53e3e; font-weight: bold;'>⏱ This code expires in <strong>5 minutes</strong>.</p>
                    <p style='color: #718096; font-size: 14px;'>If you did not register on Danation, please ignore this email.</p>
                </div>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", email);
            throw;
        }
    }

    private static string GenerateSecureOtp()
    {
        // Generate cryptographically secure 6-digit OTP
        var bytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
        return value.ToString();
    }

    public async Task<(bool Success, string ErrorMessage)> VerifyOtpAsync(VerifyOtpViewModel model)
    {
        string cacheKey = $"OTP_{model.Email.ToLower()}";

        if (!_cache.TryGetValue(cacheKey, out OtpCacheEntry? otpData) || otpData == null)
            return (false, "OTP has expired. Please request a new one.");

        if (otpData.ExpiresAt < DateTime.UtcNow)
        {
            _cache.Remove(cacheKey);
            return (false, "OTP has expired. Please request a new one.");
        }

        // Increment attempt count for brute-force protection
        otpData.Attempts++;
        if (otpData.Attempts > 5)
        {
            _cache.Remove(cacheKey);
            return (false, "Too many incorrect attempts. Please request a new OTP.");
        }

        if (otpData.Code != model.OtpCode)
            return (false, $"Incorrect OTP. {5 - otpData.Attempts} attempt(s) remaining.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
        if (user == null)
            return (false, "User account not found.");

        user.EmailVerified = true;
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _cache.Remove(cacheKey); // Invalidate OTP after successful use (single-use)

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ResendOtpAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user == null)
            return (false, "User account not found.");

        if (user.EmailVerified && user.IsActive)
            return (false, "Account is already verified and active.");

        // Throttle: prevent sending more than once per 60 seconds
        string cacheKey = $"OTP_{email.ToLower()}";
        if (_cache.TryGetValue(cacheKey, out OtpCacheEntry? existing) && existing != null)
        {
            var secondsSinceLastSend = (DateTime.UtcNow - existing.LastSentAt).TotalSeconds;
            if (secondsSinceLastSend < 60)
            {
                int waitSeconds = (int)(60 - secondsSinceLastSend);
                return (false, $"Please wait {waitSeconds} seconds before requesting another OTP.");
            }
        }

        await GenerateAndSendOtpAsync(user.Email, user.FullName);
        return (true, string.Empty);
    }

    public async Task<UserProfileViewModel?> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Email = u.Email,
                Phone = u.Phone,
                ProfileImage = u.ProfileImage,
                Role = u.Role,
                EmailVerified = u.EmailVerified,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                TotalCampaigns = u.Campaigns.Count,
                TotalDonations = u.DonationDonors.Count
            })
            .FirstOrDefaultAsync();

        return user;
    }

    public async Task<EditProfileViewModel?> GetEditProfileViewModelAsync(int userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new EditProfileViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Phone = u.Phone,
                CurrentProfileImage = u.ProfileImage
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateProfileAsync(EditProfileViewModel model, int currentUserId)
    {
        if (model.UserId != currentUserId)
            return (false, "Unauthorized profile modification attempt.");

        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null)
            return (false, "Profile not found.");

        // Handle Profile Image Upload
        if (model.NewProfileImage != null && model.NewProfileImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(model.NewProfileImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return (false, "Invalid image format. Only JPG, PNG, WEBP, and GIF files are allowed.");

            if (model.NewProfileImage.Length > 2 * 1024 * 1024)
                return (false, "Profile image file size must be less than 2MB.");

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"profile_{currentUserId}_{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.NewProfileImage.CopyToAsync(stream);
            }

            // Delete old profile image
            if (!string.IsNullOrEmpty(user.ProfileImage) && user.ProfileImage.StartsWith("/uploads/profiles/"))
            {
                string oldFilePath = Path.Combine(_env.WebRootPath, user.ProfileImage.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { /* ignore */ }
                }
            }

            user.ProfileImage = $"/uploads/profiles/{uniqueFileName}";
        }

        user.FullName = model.FullName;
        user.Phone = model.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Refresh auth cookie with updated info
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var rememberMe = bool.TryParse(httpContext.User.FindFirst("RememberMe")?.Value, out var rm) && rm;
            await _loginService.SignInUserAsync(
                user.Id.ToString(),
                user.FullName,
                user.Email,
                user.Role,
                rememberMe,
                user.ProfileImage ?? string.Empty);
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordViewModel model)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
            return (false, "Incorrect current password.");

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    /// <summary>Internal cache entry for OTP storage</summary>
    private class OtpCacheEntry
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public DateTime LastSentAt { get; set; }
    }
}
