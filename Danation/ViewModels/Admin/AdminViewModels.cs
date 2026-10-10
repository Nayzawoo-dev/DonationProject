using System.ComponentModel.DataAnnotations;
using Donation.Common;

namespace Donation.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCampaigns { get; set; }
    public int PendingCampaigns { get; set; }
    public int OpenCampaigns { get; set; }
    public int GoalReachedCampaigns { get; set; }
    public int ClosedCampaigns { get; set; }
    public int CompletedCampaigns { get; set; }
    public int PendingDonations { get; set; }
    public int ApprovedDonations { get; set; }
    public int RejectedDonations { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public decimal TotalAvailableSurplus { get; set; }
    public decimal TotalSurplusAllocated { get; set; }
    public List<AdminCampaignSummaryViewModel> RecentPendingCampaigns { get; set; } = new();
    public List<AdminDonationSummaryViewModel> RecentPendingDonations { get; set; } = new();
}

public class AdminCampaignSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CloseReason { get; set; }
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public decimal SurplusAmount { get; set; }
    public decimal AvailableSurplus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int DocumentCount { get; set; }
    public int ImageCount { get; set; }
    public string Township { get; set; } = string.Empty;
    /// <summary>Admin-only — must never be exposed in public ViewModels or API responses.</summary>
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsUpcoming => StartDate.HasValue && StartDate.Value > AppTime.Now && Status == "OPEN";
    public bool IsExpired => EndDate.HasValue && EndDate.Value <= AppTime.Now;
}

public class AdminDonationSummaryViewModel
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string DonorName { get; set; } = string.Empty;
    public string DonorEmail { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string TransferScreenshot { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByName { get; set; }
}

public class AdminApproveDonationViewModel
{
    [Required]
    public int DonationId { get; set; }

    [Required(ErrorMessage = "Verified amount is required.")]
    [Range(1, 100000000, ErrorMessage = "Amount must be greater than 0.")]
    [Display(Name = "Verified Amount (MMK)")]
    public decimal VerifiedAmount { get; set; }
}

public class AdminUserViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalCampaigns { get; set; }
    public int TotalDonations { get; set; }
}

public class AdminCreateCompletionViewModel
{
    [Required]
    public int CampaignId { get; set; }

    public string CampaignTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Caption is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Caption must be at least 10 characters.")]
    public string Caption { get; set; } = string.Empty;

    [Display(Name = "Completion Images")]
    public List<IFormFile>? CompletionImages { get; set; }
}

public class AdminEditCampaignViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(5000, MinimumLength = 50, ErrorMessage = "Description must be at least 50 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Goal amount is required.")]
    [Range(1000, 100000000, ErrorMessage = "Goal amount must be at least 1,000.")]
    [Display(Name = "Goal Amount (MMK)")]
    public decimal GoalAmount { get; set; }

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Township is required.")]
    [StringLength(100, ErrorMessage = "Township cannot exceed 100 characters.")]
    public string Township { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact phone number is required.")]
    [StringLength(30, ErrorMessage = "Contact phone cannot exceed 30 characters.")]
    [Display(Name = "Contact Phone")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Start date and time is required.")]
    [Display(Name = "Start Date & Time")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "End date and time is required.")]
    [Display(Name = "End Date & Time")]
    public DateTime? EndDate { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? CloseReason { get; set; }
    public decimal RaisedAmount { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    [Display(Name = "Explicitly Reopen Campaign (Sets status back to OPEN)")]
    public bool ReopenCampaign { get; set; }
}

public class CampaignSurplusDetailsViewModel
{
    public decimal GoalAmount { get; set; }
    public decimal TotalRaised { get; set; }
    public decimal SurplusGenerated { get; set; }
    public decimal AllocatedOut { get; set; }
    public decimal AvailableSurplus { get; set; }
    public decimal AllocatedReceived { get; set; }
}

public class AdminSurplusAllocationViewModel
{
    [Required]
    public int SourceCampaignId { get; set; }
    public string SourceCampaignTitle { get; set; } = string.Empty;
    public decimal SourceAvailableSurplus { get; set; }

    [Required(ErrorMessage = "Please select a target campaign.")]
    [Display(Name = "Target Campaign")]
    public int TargetCampaignId { get; set; }

    [Required(ErrorMessage = "Allocation amount is required.")]
    [Range(1, 100000000, ErrorMessage = "Amount must be greater than 0.")]
    [Display(Name = "Allocation Amount (MMK)")]
    public decimal Amount { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    [Display(Name = "Notes (optional)")]
    public string? Notes { get; set; }
}

public class AdminExtendCampaignDateViewModel
{
    [Required]
    public int CampaignId { get; set; }

    [Required(ErrorMessage = "Please specify the new end date.")]
    [Display(Name = "New End Date & Time")]
    public DateTime NewEndDate { get; set; }

    [Display(Name = "Also Reopen Campaign (if currently closed)")]
    public bool Reopen { get; set; }
}
