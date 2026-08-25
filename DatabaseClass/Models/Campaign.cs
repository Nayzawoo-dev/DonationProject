using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class Campaign
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal GoalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Address { get; set; } = null!;

    public string Township { get; set; } = null!;

    public string ContactPhone { get; set; } = null!;

    public virtual CampaignCompletion? CampaignCompletion { get; set; }

    public virtual ICollection<CampaignDocument> CampaignDocuments { get; set; } = new List<CampaignDocument>();

    public virtual ICollection<CampaignImage> CampaignImages { get; set; } = new List<CampaignImage>();

    public virtual ICollection<Donation> Donations { get; set; } = new List<Donation>();

    public virtual User User { get; set; } = null!;
}
