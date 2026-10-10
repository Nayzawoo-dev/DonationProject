using System;

namespace Donation.Common;

/// <summary>
/// Centralized Application Time and TimeZone Provider.
/// Establishes a consistent timezone policy:
/// - Campaign StartDate and EndDate represent Myanmar Standard Time (UTC+06:30 / Asia/Yangon).
/// - Current operational time for campaign availability, scheduling, opening, and closure is AppTime.Now.
/// - System audit timestamps (CreatedAt, UpdatedAt, etc.) continue to use UTC (AppTime.UtcNow).
/// </summary>
public static class AppTime
{
    private static readonly TimeZoneInfo _myanmarTimeZone = ResolveMyanmarTimeZone();

    private static TimeZoneInfo ResolveMyanmarTimeZone()
    {
        try
        {
            // Windows ID
            return TimeZoneInfo.FindSystemTimeZoneById("Myanmar Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // IANA ID (Linux/macOS)
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Yangon");
            }
            catch
            {
                // Guaranteed fallback for any environment
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Myanmar Standard Time",
                    TimeSpan.FromMinutes(390), // +06:30
                    "(UTC+06:30) Yangon",
                    "Myanmar Standard Time");
            }
        }
        catch
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Myanmar Standard Time",
                TimeSpan.FromMinutes(390),
                "(UTC+06:30) Yangon",
                "Myanmar Standard Time");
        }
    }

    /// <summary>
    /// Gets the resolved Myanmar TimeZoneInfo instance (UTC+06:30).
    /// </summary>
    public static TimeZoneInfo MyanmarTimeZone => _myanmarTimeZone;

    /// <summary>
    /// Current date and time in Myanmar Standard Time (UTC+06:30).
    /// Always use this when comparing campaign StartDate and EndDate.
    /// </summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _myanmarTimeZone);

    /// <summary>
    /// Current UTC date and time. Used for system audit timestamps.
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Converts a UTC DateTime to Myanmar local time.
    /// </summary>
    public static DateTime ToMyanmarTime(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _myanmarTimeZone);
    }

    /// <summary>
    /// Converts a Myanmar local DateTime to UTC.
    /// </summary>
    public static DateTime ToUtc(DateTime myanmarDateTime)
    {
        var unspecified = DateTime.SpecifyKind(myanmarDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _myanmarTimeZone);
    }
}
