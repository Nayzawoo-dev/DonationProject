using DatabaseClass.Models;
using Donation.Hubs;
using Donation.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Donation.Services;

public class CampaignClosureBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignClosureBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public CampaignClosureBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CampaignClosureBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CampaignClosureBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndCloseCampaignsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while executing campaign closure check.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("CampaignClosureBackgroundService stopped.");
    }

    public async Task CheckAndCloseCampaignsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();

        var localNow = AppTime.Now;
        var utcNow = DateTime.UtcNow;

        // Query active campaigns that might need closure
        var openCampaigns = await context.Campaigns
            .Include(c => c.Donations)
            .Where(c => c.Status == "OPEN")
            .ToListAsync(cancellationToken);

        if (!openCampaigns.Any()) return;

        bool hasChanges = false;

        foreach (var campaign in openCampaigns)
        {
            bool shouldClose = false;
            string closeReason = string.Empty;
            string notificationMessage = string.Empty;

            var directRaised = campaign.Donations
                .Where(d => d.Status == "APPROVED")
                .Sum(d => d.Amount) ?? 0;

            var allocatedSurplus = await context.CampaignSurplusTransactions
                .Where(t => t.TargetCampaignId == campaign.Id && (t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION"))
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;

            var totalRaised = directRaised + allocatedSurplus;

            // 1. If Goal has been reached before or at expiry -> GOAL_REACHED
            if (campaign.GoalAmount > 0 && totalRaised >= campaign.GoalAmount)
            {
                shouldClose = true;
                closeReason = "GOAL_REACHED";
                notificationMessage = $"Your campaign \"{campaign.Title}\" has automatically closed after reaching its goal of {campaign.GoalAmount:N0} MMK!";
            }
            // 2. If End Date has passed and Goal was NOT reached -> EXPIRED
            else if (campaign.EndDate.HasValue && campaign.EndDate.Value <= localNow)
            {
                shouldClose = true;
                closeReason = "EXPIRED";
                notificationMessage = $"Your campaign \"{campaign.Title}\" has automatically closed because its scheduled end date was reached.";
            }

            if (shouldClose)
            {
                campaign.Status = "CLOSED";
                campaign.CloseReason = closeReason;
                campaign.ClosedAt = utcNow;
                campaign.UpdatedAt = utcNow;
                hasChanges = true;

                _logger.LogInformation("Auto-closing campaign {CampaignId} ({Title}). Reason: {Reason}",
                    campaign.Id, campaign.Title, closeReason);

                // Create notification for campaign owner
                try
                {
                    await notificationService.CreateAsync(
                        campaign.UserId,
                        "Campaign Closed",
                        notificationMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create closure notification for campaign {CampaignId}", campaign.Id);
                }

                // Broadcast real-time status change
                try
                {
                    await hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
                    {
                        campaignId = campaign.Id,
                        status = "CLOSED",
                        closeReason,
                        title = campaign.Title
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast closure for campaign {CampaignId}", campaign.Id);
                }
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
            var cache = scope.ServiceProvider.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            cache?.InvalidateHomeCaches();
        }
    }
}
