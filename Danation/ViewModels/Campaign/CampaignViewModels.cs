using System.ComponentModel.DataAnnotations;

namespace Donation.ViewModels.Campaign;

public class CampaignListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerProfileImage { get; set; }
    public string? ThumbnailImage { get; set; }
    public int ImageCount { get; set; }
    public decimal ProgressPercent => GoalAmount > 0 ? Math.Min(100, Math.Round((RaisedAmount / GoalAmount) * 100, 1)) : 0;
}

public class CampaignListViewModel
{
    public List<CampaignListItemViewModel> Campaigns { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class CampaignDetailViewModel
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal ProgressPercent => GoalAmount > 0 ? Math.Min(100, Math.Round((RaisedAmount / GoalAmount) * 100, 1)) : 0;

    // Owner info
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string? OwnerProfileImage { get; set; }

    // Images & documents
    public List<CampaignImageViewModel> Images { get; set; } = new();
    public List<CampaignDocumentViewModel> Documents { get; set; } = new();

    // Completion
    public CampaignCompletionViewModel? Completion { get; set; }

    // Current user context
    public bool IsOwner { get; set; }
    public bool CanDonate { get; set; }
}

public class CampaignImageViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CampaignDocumentViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CampaignCompletionViewModel
{
    public int Id { get; set; }
    public string Caption { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public List<CompletionImageViewModel> Images { get; set; } = new();
}

public class CompletionImageViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

public class CreateCampaignViewModel
{
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
}

public class EditCampaignViewModel
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

    public string Status { get; set; } = string.Empty;

    // For image management display
    public List<CampaignImageViewModel> Images { get; set; } = new();
    public List<CampaignDocumentViewModel> Documents { get; set; } = new();
}

public class MyCampaignsViewModel
{
    public List<CampaignListItemViewModel> Campaigns { get; set; } = new();
}
