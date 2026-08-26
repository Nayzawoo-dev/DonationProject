using DatabaseClass.Models;
using Donation.ViewModels.Admin;
using Donation.ViewModels.Donation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DonationEntity = DatabaseClass.Models.Donation;

namespace Donation.Services;

public class DonationService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<DonationService> _logger;

    public DonationService(
        AppDbContext context,
        FileService fileService,
        NotificationService notificationService,
        ILogger<DonationService> logger)
    {
        _context = context;
        _fileService = fileService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<(bool Success, string ErrorMessage)> SubmitDonationAsync(
        int campaignId, int donorId, IFormFile screenshot)
    {
        // Server-side: cannot donate to own campaign
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
            return (false, "Campaign not found.");

        if (campaign.UserId == donorId)
            return (false, "You cannot donate to your own campaign.");

        if (campaign.Status != "OPEN")
            return (false, "This campaign is not currently accepting donations.");

        // Validate screenshot
        var (valid, error) = _fileService.ValidateImageFile(screenshot);
        if (!valid) return (false, error);

        var screenshotUrl = await _fileService.SaveImageAsync(screenshot, "donations");

        var donation = new DonationEntity
        {
            CampaignId = campaignId,
            DonorId = donorId,
            Amount = null, // Amount set by admin upon verification
            TransferScreenshot = screenshotUrl,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _context.Donations.Add(donation);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<MyDonationsViewModel> GetMyDonationsAsync(int userId)
    {
        var donations = await _context.Donations
            .AsNoTracking()
            .Where(d => d.DonorId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DonationHistoryItemViewModel
            {
                Id = d.Id,
                CampaignId = d.CampaignId,
                CampaignTitle = d.Campaign.Title,
                CampaignStatus = d.Campaign.Status,
                Amount = d.Amount,
                Status = d.Status,
                TransferScreenshot = d.TransferScreenshot,
                CreatedAt = d.CreatedAt,
                VerifiedAt = d.VerifiedAt,
                VerifiedByName = d.VerifiedByNavigation != null ? d.VerifiedByNavigation.FullName : null
            })
            .ToListAsync();

        return new MyDonationsViewModel { Donations = donations };
    }

    public async Task<List<AdminDonationSummaryViewModel>> GetAdminDonationsAsync(string? status)
    {
        var query = _context.Donations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new AdminDonationSummaryViewModel
            {
                Id = d.Id,
                CampaignId = d.CampaignId,
                CampaignTitle = d.Campaign.Title,
                DonorName = d.Donor.FullName,
                DonorEmail = d.Donor.Email,
                Amount = d.Amount,
                TransferScreenshot = d.TransferScreenshot,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                VerifiedAt = d.VerifiedAt,
                VerifiedByName = d.VerifiedByNavigation != null ? d.VerifiedByNavigation.FullName : null
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> ApproveDonationAsync(int donationId, decimal verifiedAmount, int adminId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var donation = await _context.Donations
                .Include(d => d.Campaign)
                .FirstOrDefaultAsync(d => d.Id == donationId);

            if (donation == null)
                return (false, "Donation not found.");

            if (donation.Status != "PENDING")
                return (false, "Only pending donations can be approved.");

            donation.Amount = verifiedAmount;
            donation.Status = "APPROVED";
            donation.VerifiedBy = adminId;
            donation.VerifiedAt = DateTime.UtcNow;

            // Check if campaign goal is reached
            var currentRaised = await _context.Donations
                .Where(d => d.CampaignId == donation.CampaignId && d.Status == "APPROVED" && d.Id != donationId)
                .SumAsync(d => (decimal?)d.Amount) ?? 0;

            var newTotal = currentRaised + verifiedAmount;
            var campaign = donation.Campaign;

            if (newTotal >= campaign.GoalAmount && campaign.Status == "OPEN")
            {
                campaign.Status = "GOAL_REACHED";
                campaign.UpdatedAt = DateTime.UtcNow;

                await _notificationService.CreateAsync(
                    campaign.UserId,
                    "🎯 Goal Reached!",
                    $"Your campaign \"{campaign.Title}\" has reached its fundraising goal of {campaign.GoalAmount:N0} MMK!");
            }

            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Notify donor
            await _notificationService.CreateAsync(
                donation.DonorId,
                "Donation Approved ✅",
                $"Your donation to \"{campaign.Title}\" has been verified and approved. Verified amount: {verifiedAmount:N0} MMK.");

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to approve donation {DonationId}", donationId);
            return (false, "An error occurred while approving the donation.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> RejectDonationAsync(int donationId, int adminId, string reason)
    {
        var donation = await _context.Donations
            .Include(d => d.Campaign)
            .FirstOrDefaultAsync(d => d.Id == donationId);

        if (donation == null) return (false, "Donation not found.");
        if (donation.Status != "PENDING") return (false, "Only pending donations can be rejected.");

        donation.Status = "REJECTED";
        donation.VerifiedBy = adminId;
        donation.VerifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(
            donation.DonorId,
            "Donation Not Approved",
            $"Your donation to \"{donation.Campaign.Title}\" could not be verified. Reason: {reason}. Please check your transfer and try again.");

        return (true, string.Empty);
    }

    public async Task<AdminDonationSummaryViewModel?> GetDonationDetailAsync(int donationId)
    {
        return await _context.Donations
            .AsNoTracking()
            .Where(d => d.Id == donationId)
            .Select(d => new AdminDonationSummaryViewModel
            {
                Id = d.Id,
                CampaignId = d.CampaignId,
                CampaignTitle = d.Campaign.Title,
                DonorName = d.Donor.FullName,
                DonorEmail = d.Donor.Email,
                Amount = d.Amount,
                TransferScreenshot = d.TransferScreenshot,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                VerifiedAt = d.VerifiedAt,
                VerifiedByName = d.VerifiedByNavigation != null ? d.VerifiedByNavigation.FullName : null
            })
            .FirstOrDefaultAsync();
    }
}
