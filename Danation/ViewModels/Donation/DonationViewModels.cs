using System.ComponentModel.DataAnnotations;

namespace Donation.ViewModels.Donation;

public class DonationSubmitViewModel
{
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public decimal CampaignGoal { get; set; }
    public decimal CampaignRaised { get; set; }

    // Payment info (from appsettings, shown to donor)
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentPhone { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please upload a transfer screenshot.")]
    [Display(Name = "Transfer Screenshot")]
    public IFormFile? TransferScreenshot { get; set; }

    [StringLength(500, ErrorMessage = "Note cannot exceed 500 characters.")]
    [Display(Name = "Note (optional)")]
    public string? Note { get; set; }
}

public class DonationHistoryItemViewModel
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TransferScreenshot { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByName { get; set; }
}

public class MyDonationsViewModel
{
    public List<DonationHistoryItemViewModel> Donations { get; set; } = new();
}
