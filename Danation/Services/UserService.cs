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

    // Cache key prefixes
    private const string OtpPrefix = "OTP_";
    private const string PwdResetPrefix = "PWD_RESET_";
    private const string PwdResetTokenPrefix = "PWD_RESET_TOKEN_";
    private const string PwdResetRatePrefix = "PWD_RESET_RATE_";

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

    // =============================================
    //  Registration & Email OTP
    // =============================================

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
        string cacheKey = $"{OtpPrefix}{email.ToLower()}";

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
        var bytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
        return value.ToString();
    }

    public async Task<(bool Success, string ErrorMessage)> VerifyOtpAsync(VerifyOtpViewModel model)
    {
        string cacheKey = $"{OtpPrefix}{model.Email.ToLower()}";

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
        _cache.Remove(cacheKey); // Single-use — invalidate after success

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
        string cacheKey = $"{OtpPrefix}{email.ToLower()}";
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

    // =============================================
    //  Forgot Password / Password Reset
    // =============================================

    /// <summary>
    /// Step 1: Request a password reset OTP.
    /// Security: Always returns a generic message — never reveals if email exists.
    /// Rate limiting: max 3 requests per email per 15 minutes.
    /// </summary>
    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email, string clientIp)
    {
        // Rate limiting — per email (3 requests per 15 min)
        string rateCacheKey = $"{PwdResetRatePrefix}{email.ToLower()}";
        int requestCount = _cache.TryGetValue(rateCacheKey, out int existingCount) ? existingCount : 0;
        if (requestCount >= 3)
        {
            // Still return generic message — do not reveal rate limit is hit
            _logger.LogWarning("ForgotPassword rate limit reached for email {Email} from IP {IP}", email, clientIp);
            return (true, "If the email is registered, a verification code has been sent.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

        // Always increment rate counter regardless of whether user exists
        _cache.Set(rateCacheKey, requestCount + 1, TimeSpan.FromMinutes(15));

        // If user doesn't exist — return generic response (no enumeration)
        if (user == null || !user.IsActive || !user.EmailVerified)
        {
            _logger.LogInformation("ForgotPassword requested for non-existent/inactive email (suppressed)");
            return (true, "If the email is registered, a verification code has been sent.");
        }

        // Invalidate any existing password reset OTP for this email
        string resetCacheKey = $"{PwdResetPrefix}{email.ToLower()}";
        _cache.Remove(resetCacheKey);

        // Generate new OTP (different key prefix from registration OTP)
        string otpCode = GenerateSecureOtp();
        var otpData = new OtpCacheEntry
        {
            Code = otpCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow
        };
        _cache.Set(resetCacheKey, otpData, TimeSpan.FromMinutes(5));

        // Send email
        string subject = "Password Reset Code — Danation Charity Platform";
        string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333;'>
                <div style='background: linear-gradient(135deg, #e53935 0%, #b71c1c 100%); padding: 30px; border-radius: 12px 12px 0 0; text-align: center;'>
                    <h1 style='color: white; margin: 0; font-size: 28px;'>🔐 Password Reset</h1>
                    <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0;'>Danation Charity Platform</p>
                </div>
                <div style='background: #fff; padding: 30px; border: 1px solid #e2e8f0; border-radius: 0 0 12px 12px;'>
                    <h2 style='color: #2d3748;'>Hello {user.FullName},</h2>
                    <p style='color: #4a5568;'>We received a request to reset your password. Use the following code:</p>
                    <div style='background: #fff5f5; font-size: 36px; font-weight: bold; letter-spacing: 10px; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0; color: #e53935; border: 2px dashed #e53935;'>
                        {otpCode}
                    </div>
                    <p style='color: #e53e3e; font-weight: bold;'>⏱ This code expires in <strong>5 minutes</strong>.</p>
                    <p style='color: #718096; font-size: 14px;'>If you did not request a password reset, please ignore this email. Your password will not be changed.</p>
                </div>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(user.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            // Return generic success — do not reveal email send failure
        }

        return (true, "If the email is registered, a verification code has been sent.");
    }

    /// <summary>
    /// Step 2: Verify the password reset OTP.
    /// On success, stores a short-lived reset token that authorizes the password change.
    /// The OTP is invalidated immediately after successful verification.
    /// </summary>
    public (bool Success, string Message, string? ResetToken) VerifyPasswordResetOtp(string email, string otpCode)
    {
        string resetCacheKey = $"{PwdResetPrefix}{email.ToLower()}";

        if (!_cache.TryGetValue(resetCacheKey, out OtpCacheEntry? otpData) || otpData == null)
            return (false, "Verification code has expired. Please request a new one.", null);

        if (otpData.ExpiresAt < DateTime.UtcNow)
        {
            _cache.Remove(resetCacheKey);
            return (false, "Verification code has expired. Please request a new one.", null);
        }

        otpData.Attempts++;
        if (otpData.Attempts > 5)
        {
            _cache.Remove(resetCacheKey);
            return (false, "Too many incorrect attempts. Please request a new code.", null);
        }

        if (otpData.Code != otpCode)
            return (false, $"Incorrect code. {5 - otpData.Attempts} attempt(s) remaining.", null);

        // OTP is correct — invalidate it (single-use)
        _cache.Remove(resetCacheKey);

        // Issue a short-lived reset token tied to this email
        string resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string tokenCacheKey = $"{PwdResetTokenPrefix}{email.ToLower()}";
        _cache.Set(tokenCacheKey, resetToken, TimeSpan.FromMinutes(10)); // 10 min to complete reset

        return (true, "Code verified. Please set your new password.", resetToken);
    }

    /// <summary>
    /// Step 3: Reset the password using the verified reset token.
    /// Token is invalidated after a single successful use.
    /// </summary>
    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        string email, string resetToken, string newPassword)
    {
        // Validate reset token
        string tokenCacheKey = $"{PwdResetTokenPrefix}{email.ToLower()}";
        if (!_cache.TryGetValue(tokenCacheKey, out string? storedToken) || storedToken != resetToken)
            return (false, "Invalid or expired reset session. Please start over.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user == null || !user.IsActive)
            return (false, "Account not found or inactive.");

        // Hash and save new password
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Invalidate reset token (single-use)
        _cache.Remove(tokenCacheKey);

        // Also clear any remaining rate limit (fresh start)
        _cache.Remove($"{PwdResetRatePrefix}{email.ToLower()}");

        _logger.LogInformation("Password successfully reset for user {Email}", email);
        return (true, "Password reset successfully. You can now log in with your new password.");
    }

    // =============================================
    //  User Profile
    // =============================================

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

    // =============================================
    //  Public Profile
    // =============================================

    /// <summary>
    /// Returns a privacy-safe public profile for the given user.
    /// Never exposes: Email, Phone, ContactPhone, donation amounts,
    /// payment screenshots, or any other private information.
    /// Uses efficient IQueryable projections and CountAsync.
    /// </summary>
    public async Task<PublicProfileViewModel?> GetPublicProfileAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive && u.EmailVerified)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Username,
                u.ProfileImage,
                u.CreatedAt,
                TotalCampaignsCreated = u.Campaigns.Count,
                TotalCampaignsDonated = u.DonationDonors
                    .Select(d => d.CampaignId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        if (user == null) return null;

        // Campaigns created — privacy-safe projection (no ContactPhone)
        var campaignsCreated = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.UserId == userId && (
                c.Status == "OPEN" || c.Status == "GOAL_REACHED" ||
                c.Status == "CLOSED" || c.Status == "COMPLETED"))
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Select(c => new PublicProfileCampaignViewModel
            {
                Id = c.Id,
                Title = c.Title,
                ThumbnailImage = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                Township = c.Township,
                Status = c.Status,
                CreatedAt = c.CreatedAt
                // ContactPhone intentionally excluded
            })
            .ToListAsync();

        // Campaigns donated to — privacy-safe projection (no amounts, no screenshots)
        var campaignsDonated = await _context.Donations
            .AsNoTracking()
            .Where(d => d.DonorId == userId && d.Status == "APPROVED")
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.CampaignId, d.Campaign.Title, d.CreatedAt,
                Thumbnail = d.Campaign.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault() })
            .Distinct()
            .Take(20)
            .ToListAsync();

        // Deduplicate by campaign (a user may donate multiple times to one campaign)
        var donatedVms = campaignsDonated
            .GroupBy(d => d.CampaignId)
            .Select(g => new PublicProfileDonationViewModel
            {
                CampaignId = g.Key,
                CampaignTitle = g.First().Title,
                CampaignThumbnail = g.First().Thumbnail,
                DonatedAt = g.Max(d => d.CreatedAt)
                // Amount, screenshot, reference intentionally excluded
            })
            .OrderByDescending(d => d.DonatedAt)
            .ToList();

        return new PublicProfileViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            ProfileImage = user.ProfileImage,
            MemberSince = user.CreatedAt,
            TotalCampaignsCreated = user.TotalCampaignsCreated,
            TotalCampaignsDonated = user.TotalCampaignsDonated,
            CampaignsCreated = campaignsCreated,
            CampaignsDonated = donatedVms
        };
    }

    // =============================================
    //  Internal OTP Cache Entry
    // =============================================

    /// <summary>Internal cache entry for OTP storage (registration and password reset).</summary>
    private class OtpCacheEntry
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public DateTime LastSentAt { get; set; }
    }
}
