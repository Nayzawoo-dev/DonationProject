using System;
using System.Collections.Generic;

namespace DatabaseClass.Models;

public partial class CompletionImage
{
    public int Id { get; set; }

    public int CompletionId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual CampaignCompletion Completion { get; set; } = null!;
}
