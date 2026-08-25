using System.ComponentModel.DataAnnotations;

namespace Donation.ViewModels.Profile;

public class UserProfileViewModel
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ProfileImage { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalCampaigns { get; set; }
    public int TotalDonations { get; set; }
}

public class EditProfileViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [Display(Name = "Phone Number")]
    public string? Phone { get; set; }

    public string? CurrentProfileImage { get; set; }

    [Display(Name = "Profile Image")]
    public IFormFile? NewProfileImage { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

// =============================================
//  Public Profile ViewModels
//  Privacy rules: NO Email, Phone, ContactPhone,
//  donation amounts, screenshots, or private data
// =============================================

/// <summary>Privacy-safe view of a campaign for the public profile page.</summary>
public class PublicProfileCampaignViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailImage { get; set; }
    public string Township { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    // ContactPhone intentionally excluded
}

/// <summary>Privacy-safe view of a donation activity for the public profile page.</summary>
public class PublicProfileDonationViewModel
{
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string? CampaignThumbnail { get; set; }
    public DateTime DonatedAt { get; set; }
    // Amount, screenshot, reference — intentionally excluded for privacy
}

/// <summary>
/// Privacy-safe public profile ViewModel.
/// Must NEVER expose: Email, Phone, ContactPhone, PasswordHash,
/// donation amounts, payment screenshots, or any private information.
/// </summary>
public class PublicProfileViewModel
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public DateTime MemberSince { get; set; }
    public int TotalCampaignsCreated { get; set; }
    public int TotalCampaignsDonated { get; set; }

    // Paginated activity sections
    public List<PublicProfileCampaignViewModel> CampaignsCreated { get; set; } = new();
    public List<PublicProfileDonationViewModel> CampaignsDonated { get; set; } = new();
}
