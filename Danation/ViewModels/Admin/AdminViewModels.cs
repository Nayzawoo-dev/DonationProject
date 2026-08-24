using System.ComponentModel.DataAnnotations;

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
    public List<AdminCampaignSummaryViewModel> RecentPendingCampaigns { get; set; } = new();
    public List<AdminDonationSummaryViewModel> RecentPendingDonations { get; set; } = new();
}

public class AdminCampaignSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int DocumentCount { get; set; }
    public int ImageCount { get; set; }
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
