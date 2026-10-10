using Microsoft.Extensions.Caching.Memory;

namespace Donation.Common;

/// <summary>
/// Centralized Cache Keys and Invalidation Helpers.
/// </summary>
public static class CacheKeys
{
    public const string HomeLandingStats = "HOME_LANDING_STATS";
    public const string HomeRecentCampaigns = "HOME_RECENT_CAMPAIGNS";

    /// <summary>
    /// Invalidates all landing page cached entries when campaign data, donations, or stats change.
    /// </summary>
    public static void InvalidateHomeCaches(this IMemoryCache? cache)
    {
        if (cache == null) return;
        cache.Remove(HomeLandingStats);
        cache.Remove(HomeRecentCampaigns);
    }
}
