using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class CampaignSurplusTransaction
{
    public long SurplusTransactionId { get; set; }

    public int SourceCampaignId { get; set; }

    public int? TargetCampaignId { get; set; }

    public string TransactionType { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime TransactionDate { get; set; }

    public string? Notes { get; set; }

    public virtual Campaign SourceCampaign { get; set; } = null!;

    public virtual Campaign? TargetCampaign { get; set; }
}
