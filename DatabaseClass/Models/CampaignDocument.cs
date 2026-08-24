using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class CampaignDocument
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? DocumentType { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;
}
