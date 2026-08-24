using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class Donation
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public int DonorId { get; set; }

    public decimal? Amount { get; set; }

    public string TransferScreenshot { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual User Donor { get; set; } = null!;

    public virtual User? VerifiedByNavigation { get; set; }
}
