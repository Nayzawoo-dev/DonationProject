using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class CampaignImage
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;
}
