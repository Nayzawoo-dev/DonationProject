using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class CampaignCompletion
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public string Caption { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual ICollection<CompletionImage> CompletionImages { get; set; } = new List<CompletionImage>();

    public virtual User CreatedByNavigation { get; set; } = null!;
}
