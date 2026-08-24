using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string? ProfileImage { get; set; }

    public string Role { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CampaignCompletion> CampaignCompletions { get; set; } = new List<CampaignCompletion>();

    public virtual ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();

    public virtual ICollection<Donation> DonationDonors { get; set; } = new List<Donation>();

    public virtual ICollection<Donation> DonationVerifiedByNavigations { get; set; } = new List<Donation>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
