using DatabaseClass.Models;
using Donation.Hubs;
using Donation.Common;
using Donation.ViewModels.Admin;
using Donation.ViewModels.Campaign;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Donation.Services;

public class CampaignService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly NotificationService _notificationService;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CampaignService> _logger;
    private const int PageSize = 9;

    public CampaignService(
        AppDbContext context,
        FileService fileService,
        NotificationService notificationService,
        IHubContext<AppHub> hubContext,
        IMemoryCache cache,
        ILogger<CampaignService> logger)
    {
        _context = context;
        _fileService = fileService;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Public campaign listing with search, status, and township filters.
    /// ContactPhone is NEVER included in the projection — public-safe only.
    /// </summary>
    public async Task<CampaignListViewModel> GetPublicCampaignsAsync(
        string? search, string? status, string? township, int page = 1)
    {
        var query = _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == "OPEN" || c.Status == "GOAL_REACHED" || c.Status == "CLOSED" || c.Status == "COMPLETED")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        // Township filter — server-side, case-insensitive contains
        if (!string.IsNullOrWhiteSpace(township))
            query = query.Where(c => c.Township.Contains(township));

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / PageSize);

        var rawCampaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Description,
                c.GoalAmount,
                DirectApproved = c.Donations
                    .Where(d => d.Status == "APPROVED")
                    .Sum(d => (decimal?)d.Amount) ?? 0,
                AllocatedIn = c.CampaignSurplusTransactionTargetCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                AllocatedOut = c.CampaignSurplusTransactionSourceCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                c.Status,
                c.CreatedAt,
                c.StartDate,
                c.EndDate,
                OwnerId = c.UserId,
                OwnerName = c.User.FullName,
                OwnerProfileImage = c.User.ProfileImage,
                ThumbnailImage = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                ImageCount = c.CampaignImages.Count,
                c.Township
            })
            .ToListAsync();

        var campaigns = rawCampaigns.Select(c =>
        {
            var totalRaised = c.DirectApproved + c.AllocatedIn;
            decimal surplusAmount = 0;
            if (c.Status == "COMPLETED")
            {
                var grossSurplus = Math.Max(0, totalRaised - c.GoalAmount);
                surplusAmount = Math.Max(0, grossSurplus - c.AllocatedOut);
            }

            return new CampaignListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                RaisedAmount = totalRaised,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                SurplusAmount = surplusAmount,
                OwnerId = c.OwnerId,
                OwnerName = c.OwnerName,
                OwnerProfileImage = c.OwnerProfileImage,
                ThumbnailImage = c.ThumbnailImage,
                ImageCount = c.ImageCount,
                Township = c.Township
            };
        }).ToList();

        return new CampaignListViewModel
        {
            Campaigns = campaigns,
            SearchTerm = search,
            StatusFilter = status,
            TownshipFilter = township,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Public campaign detail.
    /// ContactPhone is NEVER included — use GetDetailForAdminAsync for admin.
    /// </summary>
    public async Task<CampaignDetailViewModel?> GetDetailAsync(int id, int? currentUserId)
    {
        var campaign = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CampaignDetailViewModel
            {
                Id = c.Id,
                OwnerId = c.UserId,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                RaisedAmount = (c.Donations
                    .Where(d => d.Status == "APPROVED")
                    .Sum(d => (decimal?)d.Amount) ?? 0)
                    + (c.CampaignSurplusTransactionTargetCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0),
                AllocatedSurplusReceived = c.CampaignSurplusTransactionTargetCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                SurplusAmount = 0,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                ClosedAt = c.ClosedAt,
                CompletedAt = c.CompletedAt,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CloseReason = c.CloseReason,
                Address = c.Address,
                Township = c.Township,
                // ContactPhone intentionally excluded from public projection
                OwnerName = c.User.FullName,
                OwnerUsername = c.User.Username,
                OwnerProfileImage = c.User.ProfileImage,
                Images = c.CampaignImages
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new CampaignImageViewModel
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        Caption = i.Caption,
                        CreatedAt = i.CreatedAt
                    }).ToList(),
                Completion = c.CampaignCompletion == null ? null : new CampaignCompletionViewModel
                {
                    Id = c.CampaignCompletion.Id,
                    Caption = c.CampaignCompletion.Caption,
                    CreatedAt = c.CampaignCompletion.CreatedAt,
                    CreatedByName = c.CampaignCompletion.CreatedByNavigation.FullName,
                    Images = c.CampaignCompletion.CompletionImages
                        .OrderBy(i => i.CreatedAt)
                        .Select(i => new CompletionImageViewModel
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            Caption = i.Caption
                        }).ToList()
                }
            })
            .FirstOrDefaultAsync();

        if (campaign == null) return null;

        if (campaign.Status == "COMPLETED")
        {
            var allocatedOut = await _context.CampaignSurplusTransactions
                .Where(t => t.SourceCampaignId == id && (t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION"))
                .SumAsync(t => (decimal?)t.Amount) ?? 0;
            var grossSurplus = Math.Max(0, campaign.RaisedAmount - campaign.GoalAmount);
            campaign.SurplusAmount = Math.Max(0, grossSurplus - allocatedOut);
        }
        else
        {
            campaign.SurplusAmount = 0;
        }

        var now = AppTime.Now;
        bool isUpcoming = campaign.StartDate.HasValue && campaign.StartDate.Value > now;
        bool isExpired = campaign.EndDate.HasValue && campaign.EndDate.Value <= now;
        bool isGoalReached = campaign.GoalAmount > 0 && campaign.RaisedAmount >= campaign.GoalAmount;

        campaign.IsOwner = currentUserId.HasValue && campaign.OwnerId == currentUserId.Value;

        // Campaign owner CAN now donate to their own campaign (Requirement supersedes previous rule)
        campaign.CanDonate = currentUserId.HasValue
            && campaign.Status == "OPEN"
            && !isUpcoming
            && !isExpired
            && !isGoalReached;

        return campaign;
    }

    /// <summary>
    /// Public campaign detail + campaign documents (for edit view / owner).
    /// ContactPhone is NOT exposed here — admin only.
    /// </summary>
    public async Task<CampaignDetailViewModel?> GetDetailWithDocsAsync(int id, int currentUserId)
    {
        var campaign = await GetDetailAsync(id, currentUserId);
        if (campaign == null) return null;

        campaign.Documents = await _context.CampaignDocuments
            .AsNoTracking()
            .Where(d => d.CampaignId == id)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new CampaignDocumentViewModel
            {
                Id = d.Id,
                ImageUrl = d.ImageUrl,
                DocumentType = d.DocumentType,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return campaign;
    }

    /// <summary>
    /// Creates a new Campaign with StartDate, EndDate, Address, Township, ContactPhone.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage, int CampaignId)> CreateAsync(
        ViewModels.Campaign.CreateCampaignViewModel model, int userId)
    {
        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
            return (false, "Both start date and end date are required.", 0);

        if (model.EndDate.Value <= model.StartDate.Value)
            return (false, "End date must be later than start date.", 0);

        if (model.EndDate.Value <= AppTime.Now)
            return (false, "End date must be in the future.", 0);

        var campaign = new Campaign
        {
            UserId = userId,
            Title = model.Title,
            Description = model.Description,
            GoalAmount = model.GoalAmount,
            Address = model.Address,
            Township = model.Township,
            ContactPhone = model.ContactPhone,
            StartDate = model.StartDate.Value,
            EndDate = model.EndDate.Value,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        // Real-time notify Admins of new pending campaign
        try
        {
            var ownerName = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "User";
            var pendingCount = await _context.Campaigns.CountAsync(c => c.Status == "PENDING");

            await _hubContext.Clients.Group(AppHub.AdminGroup).SendAsync("CampaignCreated", new
            {
                id = campaign.Id,
                title = campaign.Title,
                ownerName,
                goalAmount = campaign.GoalAmount,
                createdAt = campaign.CreatedAt.ToString("o"),
                pendingCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast CampaignCreated for campaign {CampaignId}", campaign.Id);
        }

        return (true, string.Empty, campaign.Id);
    }

    /// <summary>Loads the edit ViewModel including Address, Township, ContactPhone for the owner (PENDING only).</summary>
    public async Task<EditCampaignViewModel?> GetEditViewModelAsync(int id, int currentUserId)
    {
        return await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == id && c.UserId == currentUserId && c.Status == "PENDING")
            .Select(c => new EditCampaignViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                Address = c.Address,
                Township = c.Township,
                ContactPhone = c.ContactPhone,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                CloseReason = c.CloseReason,
                Images = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => new CampaignImageViewModel
                {
                    Id = i.Id, ImageUrl = i.ImageUrl, Caption = i.Caption, CreatedAt = i.CreatedAt
                }).ToList(),
                Documents = c.CampaignDocuments.OrderBy(d => d.CreatedAt).Select(d => new CampaignDocumentViewModel
                {
                    Id = d.Id, ImageUrl = d.ImageUrl, DocumentType = d.DocumentType, CreatedAt = d.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>Updates an owned campaign (only allowed while PENDING).</summary>
    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(EditCampaignViewModel model, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == model.Id && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found or you do not have permission to edit it.");

        if (campaign.Status != "PENDING")
            return (false, "This campaign can no longer be edited because it has already been approved or is no longer pending.");

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
            return (false, "Both start date and end date are required.");

        if (model.EndDate.Value <= model.StartDate.Value)
            return (false, "End date must be later than start date.");

        if (model.EndDate.Value <= AppTime.Now)
            return (false, "End date must be in the future.");

        campaign.Title = model.Title;
        campaign.Description = model.Description;
        campaign.GoalAmount = model.GoalAmount;
        campaign.Address = model.Address;
        campaign.Township = model.Township;
        campaign.ContactPhone = model.ContactPhone;
        campaign.StartDate = model.StartDate.Value;
        campaign.EndDate = model.EndDate.Value;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAsync(int id, int currentUserId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.CampaignImages)
            .Include(c => c.CampaignDocuments)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUserId);

        if (campaign == null)
            return (false, "Campaign not found or you do not have permission.");

        if (campaign.Status != "PENDING")
            return (false, "Only PENDING campaigns can be deleted.");

        // Delete associated files
        foreach (var img in campaign.CampaignImages)
            _fileService.DeleteFile(img.ImageUrl);
        foreach (var doc in campaign.CampaignDocuments)
            _fileService.DeleteFile(doc.ImageUrl);

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage, CampaignImageViewModel? Image)> UploadImageAsync(
        int campaignId, IFormFile file, string? caption, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found.", null);

        if (campaign.Status != "PENDING")
            return (false, "Campaign images cannot be modified after approval.", null);

        var (valid, error) = _fileService.ValidateImageFile(file);
        if (!valid) return (false, error, null);

        var imageUrl = await _fileService.SaveImageAsync(file, "campaigns");

        var image = new CampaignImage
        {
            CampaignId = campaignId,
            ImageUrl = imageUrl,
            Caption = caption,
            CreatedAt = DateTime.UtcNow
        };
        _context.CampaignImages.Add(image);
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        return (true, string.Empty, new CampaignImageViewModel
        {
            Id = image.Id,
            ImageUrl = image.ImageUrl,
            Caption = image.Caption,
            CreatedAt = image.CreatedAt
        });
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteImageAsync(int imageId, int currentUserId)
    {
        var image = await _context.CampaignImages
            .Include(i => i.Campaign)
            .FirstOrDefaultAsync(i => i.Id == imageId);

        if (image == null) return (false, "Image not found.");
        if (image.Campaign.UserId != currentUserId) return (false, "Unauthorized.");

        if (image.Campaign.Status != "PENDING")
            return (false, "Campaign images cannot be modified after approval.");

        _fileService.DeleteFile(image.ImageUrl);
        _context.CampaignImages.Remove(image);
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage, CampaignDocumentViewModel? Document)> UploadDocumentAsync(
        int campaignId, IFormFile file, string? documentType, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found.", null);

        if (campaign.Status != "PENDING")
            return (false, "Campaign documents cannot be modified after approval.", null);

        var (valid, error) = _fileService.ValidateDocumentFile(file);
        if (!valid) return (false, error, null);

        var docUrl = await _fileService.SaveImageAsync(file, "documents");

        var doc = new CampaignDocument
        {
            CampaignId = campaignId,
            ImageUrl = docUrl,
            DocumentType = documentType,
            CreatedAt = DateTime.UtcNow
        };
        _context.CampaignDocuments.Add(doc);
        await _context.SaveChangesAsync();

        return (true, string.Empty, new CampaignDocumentViewModel
        {
            Id = doc.Id,
            ImageUrl = doc.ImageUrl,
            DocumentType = doc.DocumentType,
            CreatedAt = doc.CreatedAt
        });
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteDocumentAsync(int documentId, int currentUserId)
    {
        var doc = await _context.CampaignDocuments
            .Include(d => d.Campaign)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (doc == null) return (false, "Document not found.");
        if (doc.Campaign.UserId != currentUserId) return (false, "Unauthorized.");

        if (doc.Campaign.Status != "PENDING")
            return (false, "Campaign documents cannot be modified after approval.");

        _fileService.DeleteFile(doc.ImageUrl);
        _context.CampaignDocuments.Remove(doc);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<List<CampaignListItemViewModel>> GetUserCampaignsAsync(int userId)
    {
        return await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CampaignListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                RaisedAmount = (c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0)
                    + (c.CampaignSurplusTransactionTargetCampaigns.Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => (decimal?)t.Amount) ?? 0),
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CloseReason = c.CloseReason,
                SurplusAmount = 0, // Computed post-query per-campaign only for COMPLETED campaigns
                OwnerId = c.UserId,
                OwnerName = c.User.FullName,
                ThumbnailImage = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                ImageCount = c.CampaignImages.Count,
                Township = c.Township
            })
            .ToListAsync();
    }

    // =============================================
    //  Admin Methods
    // =============================================

    public async Task<(bool Success, string ErrorMessage)> ApproveCampaignAsync(int campaignId, int adminId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return (false, "Campaign not found.");
        if (campaign.Status != "PENDING") return (false, "Only PENDING campaigns can be approved.");

        campaign.Status = "OPEN";
        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Approved! 🎉",
            $"Your campaign \"{campaign.Title}\" has been approved and is now live for donations.");

        // Real-time broadcast status change
        try
        {
            await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
            {
                campaignId = campaign.Id,
                status = "OPEN",
                title = campaign.Title
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast CampaignStatusChanged for {CampaignId}", campaignId);
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RejectCampaignAsync(int campaignId, int adminId, string reason)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return (false, "Campaign not found.");
        if (campaign.Status != "PENDING") return (false, "Only PENDING campaigns can be rejected.");

        campaign.Status = "REJECTED";
        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Not Approved",
            $"Your campaign \"{campaign.Title}\" was not approved. Reason: {reason}");

        // Real-time broadcast status change to owner & admins
        try
        {
            await _hubContext.Clients.User(campaign.UserId.ToString()).SendAsync("CampaignStatusChanged", new
            {
                campaignId = campaign.Id,
                status = "REJECTED",
                title = campaign.Title,
                reason
            });
            await _hubContext.Clients.Group(AppHub.AdminGroup).SendAsync("CampaignStatusChanged", new
            {
                campaignId = campaign.Id,
                status = "REJECTED",
                title = campaign.Title,
                reason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast CampaignStatusChanged for {CampaignId}", campaignId);
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> CloseCampaignAsync(int campaignId, int adminId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return (false, "Campaign not found.");
        if (campaign.Status == "CLOSED" || campaign.Status == "COMPLETED")
            return (false, "Campaign is already closed or completed.");

        campaign.Status = "CLOSED";
        campaign.CloseReason = "ADMIN_CLOSED";
        campaign.ClosedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Closed",
            $"Your campaign \"{campaign.Title}\" has been closed by an administrator.");

        // Real-time broadcast status change to all clients
        try
        {
            await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
            {
                campaignId = campaign.Id,
                status = "CLOSED",
                closeReason = "ADMIN_CLOSED",
                title = campaign.Title
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast CampaignStatusChanged for {CampaignId}", campaignId);
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Admin campaign listing — includes ContactPhone (admin-only field), dates, and surplus.
    /// This data must NEVER be returned to public-facing endpoints.
    /// </summary>
    public async Task<List<AdminCampaignSummaryViewModel>> GetAdminCampaignsAsync(string? status, string? search)
    {
        var query = _context.Campaigns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.Contains(search) || c.User.FullName.Contains(search));

        var rawList = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Status,
                c.CloseReason,
                c.GoalAmount,
                DirectApproved = c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0,
                AllocatedIn = c.CampaignSurplusTransactionTargetCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                AllocatedOut = c.CampaignSurplusTransactionSourceCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                c.StartDate,
                c.EndDate,
                OwnerName = c.User.FullName,
                OwnerUsername = c.User.Username,
                c.CreatedAt,
                DocumentCount = c.CampaignDocuments.Count,
                ImageCount = c.CampaignImages.Count,
                c.Township,
                c.ContactPhone
            })
            .ToListAsync();

        return rawList.Select(c =>
        {
            var totalRaised = c.DirectApproved + c.AllocatedIn;
            decimal surplusAmount = 0;
            decimal availableSurplus = 0;
            if (c.Status == "COMPLETED")
            {
                var grossSurplus = Math.Max(0, totalRaised - c.GoalAmount);
                surplusAmount = grossSurplus;
                availableSurplus = Math.Max(0, grossSurplus - c.AllocatedOut);
            }
            return new AdminCampaignSummaryViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Status = c.Status,
                CloseReason = c.CloseReason,
                GoalAmount = c.GoalAmount,
                RaisedAmount = totalRaised,
                SurplusAmount = surplusAmount,
                AvailableSurplus = availableSurplus,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                OwnerName = c.OwnerName,
                OwnerUsername = c.OwnerUsername,
                CreatedAt = c.CreatedAt,
                DocumentCount = c.DocumentCount,
                ImageCount = c.ImageCount,
                Township = c.Township,
                ContactPhone = c.ContactPhone // Admin-only field
            };
        }).ToList();
    }

    /// <summary>
    /// Loads campaign details for Admin editing.
    /// </summary>
    public async Task<AdminEditCampaignViewModel?> GetAdminEditCampaignViewModelAsync(int id)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.User)
            .Include(c => c.Donations)
            .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null) return null;

        var directRaised = campaign.Donations.Where(d => d.Status == "APPROVED").Sum(d => d.Amount) ?? 0;
        var allocatedIn = campaign.CampaignSurplusTransactionTargetCampaigns
            .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);

        return new AdminEditCampaignViewModel
        {
            Id = campaign.Id,
            Title = campaign.Title,
            Description = campaign.Description,
            GoalAmount = campaign.GoalAmount,
            Address = campaign.Address,
            Township = campaign.Township,
            ContactPhone = campaign.ContactPhone,
            StartDate = campaign.StartDate,
            EndDate = campaign.EndDate,
            Status = campaign.Status,
            CloseReason = campaign.CloseReason,
            RaisedAmount = directRaised + allocatedIn,
            OwnerName = campaign.User?.FullName ?? campaign.User?.Username ?? "Campaign Owner"
        };
    }

    /// <summary>
    /// Admin can edit all permitted campaign properties and optionally reopen a campaign.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> AdminUpdateCampaignAsync(
        AdminEditCampaignViewModel model, int adminId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Donations)
            .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
            .FirstOrDefaultAsync(c => c.Id == model.Id);

        if (campaign == null) return (false, "Campaign not found.");

        // COMPLETED campaigns are permanently locked — no edits or reopening allowed
        if (campaign.Status == "COMPLETED")
            return (false, "Completed campaigns cannot be modified or reopened.");

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
            return (false, "Both start date and end date are required.");

        if (model.EndDate.Value <= model.StartDate.Value)
            return (false, "End date must be later than start date.");

        campaign.Title = model.Title;
        campaign.Description = model.Description;
        campaign.GoalAmount = model.GoalAmount;
        campaign.Address = model.Address;
        campaign.Township = model.Township;
        campaign.ContactPhone = model.ContactPhone;
        campaign.StartDate = model.StartDate.Value;
        campaign.EndDate = model.EndDate.Value;
        campaign.UpdatedAt = DateTime.UtcNow;

        // Check explicit reopen request
        if (model.ReopenCampaign && campaign.Status != "OPEN")
        {
            var directRaised = campaign.Donations.Where(d => d.Status == "APPROVED").Sum(d => d.Amount) ?? 0;
            var allocatedIn = campaign.CampaignSurplusTransactionTargetCampaigns
                .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);
            var totalRaised = directRaised + allocatedIn;

            if (model.EndDate.Value <= AppTime.Now)
            {
                return (false, "Cannot reopen campaign with an end date in the past. Please specify a future end date.");
            }

            if (model.GoalAmount > 0 && totalRaised >= model.GoalAmount)
            {
                return (false, "Campaign has reached its goal amount. To reopen, please increase the goal amount.");
            }

            campaign.Status = "OPEN";
            campaign.CloseReason = null;
            campaign.ClosedAt = null;

            await _notificationService.CreateAsync(
                campaign.UserId,
                "Campaign Reopened",
                $"Your campaign \"{campaign.Title}\" has been updated and reopened by an administrator.");

            try
            {
                await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
                {
                    campaignId = campaign.Id,
                    status = "OPEN",
                    title = campaign.Title
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast reopen for campaign {CampaignId}", campaign.Id);
            }
        }

        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();
        return (true, string.Empty);
    }

    /// <summary>
    /// Admin extends a campaign's end date. Extending date does NOT automatically reopen unless reopen is explicitly requested.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> AdminExtendEndDateAsync(
        int campaignId, DateTime newEndDate, bool reopen, int adminId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Donations)
            .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null) return (false, "Campaign not found.");

        // COMPLETED campaigns are permanently locked
        if (campaign.Status == "COMPLETED")
            return (false, "Completed campaigns cannot be extended or reopened.");

        if (campaign.StartDate.HasValue && newEndDate <= campaign.StartDate.Value)
            return (false, "New end date must be later than the campaign start date.");

        if (newEndDate <= AppTime.Now)
            return (false, "New end date must be in the future.");

        campaign.EndDate = newEndDate;
        campaign.UpdatedAt = DateTime.UtcNow;

        if (reopen && campaign.Status == "CLOSED")
        {
            var directRaised = campaign.Donations.Where(d => d.Status == "APPROVED").Sum(d => d.Amount) ?? 0;
            var allocatedIn = campaign.CampaignSurplusTransactionTargetCampaigns
                .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);
            var totalRaised = directRaised + allocatedIn;

            if (campaign.GoalAmount > 0 && totalRaised >= campaign.GoalAmount)
            {
                return (false, "Cannot reopen a goal-reached campaign without increasing its goal amount.");
            }

            campaign.Status = "OPEN";
            campaign.CloseReason = null;
            campaign.ClosedAt = null;

            await _notificationService.CreateAsync(
                campaign.UserId,
                "Campaign Extended & Reopened",
                $"Your campaign \"{campaign.Title}\" has been extended to {newEndDate:MMM d, yyyy} and reopened.");

            try
            {
                await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
                {
                    campaignId = campaign.Id,
                    status = "OPEN",
                    title = campaign.Title
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast reopen for campaign {CampaignId}", campaign.Id);
            }
        }

        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();
        return (true, string.Empty);
    }

    /// <summary>
    /// Explicit Admin-authorized reopening of a closed/expired campaign.
    /// Supports optional newEndDate to atomically extend and reopen in a single operation.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> AdminReopenCampaignAsync(int campaignId, int adminId, DateTime? newEndDate = null)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Donations)
            .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null) return (false, "Campaign not found.");
        // COMPLETED campaigns are permanently locked — reopening is prohibited
        if (campaign.Status == "COMPLETED") return (false, "Completed campaigns cannot be reopened.");
        if (campaign.Status == "OPEN") return (false, "Campaign is already OPEN.");

        if (newEndDate.HasValue)
        {
            if (newEndDate.Value <= AppTime.Now)
                return (false, "New end date must be in the future.");

            if (campaign.StartDate.HasValue && newEndDate.Value <= campaign.StartDate.Value)
                return (false, "New end date must be later than the campaign start date.");

            campaign.EndDate = newEndDate.Value;
        }
        else if (campaign.EndDate.HasValue && campaign.EndDate.Value <= AppTime.Now)
        {
            return (false, "Cannot reopen an expired campaign. Please extend the end date first.");
        }

        var directRaised = campaign.Donations.Where(d => d.Status == "APPROVED").Sum(d => d.Amount) ?? 0;
        var allocatedIn = campaign.CampaignSurplusTransactionTargetCampaigns
            .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);
        var totalRaised = directRaised + allocatedIn;

        if (campaign.GoalAmount > 0 && totalRaised >= campaign.GoalAmount)
            return (false, "Cannot reopen a goal-reached campaign without increasing its goal amount.");

        campaign.Status = "OPEN";
        campaign.CloseReason = null;
        campaign.ClosedAt = null;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _cache.InvalidateHomeCaches();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Reopened",
            $"Your campaign \"{campaign.Title}\" has been explicitly reopened by an administrator.");

        try
        {
            await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
            {
                campaignId = campaign.Id,
                status = "OPEN",
                title = campaign.Title
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast reopen for campaign {CampaignId}", campaign.Id);
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Calculates the campaign surplus details: Goal, TotalRaised, SurplusGenerated, AllocatedOut, AvailableSurplus.
    /// </summary>
    public async Task<CampaignSurplusDetailsViewModel> GetCampaignSurplusDetailsAsync(int campaignId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Donations)
            .Include(c => c.CampaignSurplusTransactionSourceCampaigns)
            .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null) return new CampaignSurplusDetailsViewModel();

        var directApproved = campaign.Donations.Where(d => d.Status == "APPROVED").Sum(d => d.Amount) ?? 0;
        var allocatedIn = campaign.CampaignSurplusTransactionTargetCampaigns
            .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);
        var totalRaised = directApproved + allocatedIn;

        // Surplus base is total eligible funding (direct + incoming allocations), not direct-only
        var surplusGenerated = (campaign.Status == "COMPLETED")
            ? Math.Max(0, totalRaised - campaign.GoalAmount)
            : 0;
        var allocatedOut = campaign.CampaignSurplusTransactionSourceCampaigns
            .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => t.Amount);
        var availableSurplus = Math.Max(0, surplusGenerated - allocatedOut);

        return new CampaignSurplusDetailsViewModel
        {
            GoalAmount = campaign.GoalAmount,
            TotalRaised = totalRaised,
            SurplusGenerated = surplusGenerated,
            AllocatedOut = allocatedOut,
            AvailableSurplus = availableSurplus,
            AllocatedReceived = allocatedIn
        };
    }

    /// <summary>
    /// Calculates total available surplus and total surplus allocated across all campaigns.
    /// </summary>
    public async Task<(decimal TotalAvailableSurplus, decimal TotalSurplusAllocated)> GetAdminTotalSurplusStatsAsync()
    {
        // Only COMPLETED campaigns contribute to surplus pool
        var campaigns = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == "COMPLETED")
            .Select(c => new
            {
                c.Id,
                c.GoalAmount,
                DirectApproved = c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0,
                AllocatedIn = c.CampaignSurplusTransactionTargetCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                AllocatedOut = c.CampaignSurplusTransactionSourceCampaigns
                    .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                    .Sum(t => (decimal?)t.Amount) ?? 0
            })
            .ToListAsync();

        decimal totalAvailable = 0;
        decimal totalAllocated = 0;

        foreach (var c in campaigns)
        {
            var totalRaised = c.DirectApproved + c.AllocatedIn;
            var surplusGenerated = Math.Max(0, totalRaised - c.GoalAmount);
            var available = Math.Max(0, surplusGenerated - c.AllocatedOut);
            totalAvailable += available;
            totalAllocated += c.AllocatedOut;
        }

        return (totalAvailable, totalAllocated);
    }

    /// <summary>
    /// Returns eligible target campaigns for receiving surplus funds.
    /// Must be OPEN, not expired, and have remaining funding needed (not reached goal).
    /// </summary>
    public async Task<List<CampaignListItemViewModel>> GetEligibleTargetCampaignsAsync(int sourceCampaignId)
    {
        var now = AppTime.Now;
        var eligible = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id != sourceCampaignId 
                     && c.Status == "OPEN"
                     && (!c.EndDate.HasValue || c.EndDate.Value > now))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CampaignListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                GoalAmount = c.GoalAmount,
                RaisedAmount = (c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0)
                    + (c.CampaignSurplusTransactionTargetCampaigns.Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION").Sum(t => (decimal?)t.Amount) ?? 0),
                Status = c.Status,
                OwnerName = c.User.FullName,
                Township = c.Township
            })
            .ToListAsync();

        return eligible
            .Where(c => c.GoalAmount <= 0 || c.RaisedAmount < c.GoalAmount)
            .ToList();
    }

    /// <summary>
    /// Allocates surplus funds from a source campaign to an eligible target campaign atomically.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> AllocateSurplusAsync(
        int sourceCampaignId, int targetCampaignId, decimal amount, string? notes, int adminId)
    {
        if (amount <= 0)
            return (false, "Allocation amount must be greater than zero.");

        if (sourceCampaignId == targetCampaignId)
            return (false, "Source campaign and target campaign cannot be the same.");

        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var sourceCampaign = await _context.Campaigns
                .Include(c => c.Donations)
                .Include(c => c.CampaignSurplusTransactionSourceCampaigns)
                .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
                .FirstOrDefaultAsync(c => c.Id == sourceCampaignId);

            if (sourceCampaign == null)
                return (false, "Source campaign not found.");

            // Only COMPLETED campaigns may distribute surplus
            if (sourceCampaign.Status != "COMPLETED")
                return (false, $"Only COMPLETED campaigns can distribute surplus (current status: {sourceCampaign.Status}).");

            var targetCampaign = await _context.Campaigns
                .Include(c => c.Donations)
                .Include(c => c.CampaignSurplusTransactionTargetCampaigns)
                .FirstOrDefaultAsync(c => c.Id == targetCampaignId);

            if (targetCampaign == null)
                return (false, "Target campaign not found.");

            if (targetCampaign.Status != "OPEN")
                return (false, $"Target campaign is not currently OPEN for funding (current status: {targetCampaign.Status}).");

            if (targetCampaign.EndDate.HasValue && targetCampaign.EndDate.Value <= AppTime.Now)
                return (false, "Target campaign has expired and cannot receive funding.");

            // Calculate source available surplus atomically (includes incoming allocations)
            var sourceDirect = sourceCampaign.Donations
                .Where(d => d.Status == "APPROVED")
                .Sum(d => d.Amount) ?? 0;
            var sourceAllocatedIn = sourceCampaign.CampaignSurplusTransactionTargetCampaigns
                .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                .Sum(t => t.Amount);
            var sourceTotalRaised = sourceDirect + sourceAllocatedIn;
            var sourceSurplusGenerated = Math.Max(0, sourceTotalRaised - sourceCampaign.GoalAmount);
            var sourceAllocatedOut = sourceCampaign.CampaignSurplusTransactionSourceCampaigns
                .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                .Sum(t => t.Amount);
            var availableSurplus = Math.Max(0, sourceSurplusGenerated - sourceAllocatedOut);

            if (amount > availableSurplus)
            {
                return (false, $"Requested allocation ({amount:N0} MMK) exceeds available surplus ({availableSurplus:N0} MMK).");
            }

            // Calculate target campaign funding
            var targetDirect = targetCampaign.Donations
                .Where(d => d.Status == "APPROVED")
                .Sum(d => d.Amount) ?? 0;
            var targetAllocatedIn = targetCampaign.CampaignSurplusTransactionTargetCampaigns
                .Where(t => t.TransactionType == "FundAllocated" || t.TransactionType == "ALLOCATION")
                .Sum(t => t.Amount);
            var targetTotalRaised = targetDirect + targetAllocatedIn;

            if (targetCampaign.GoalAmount > 0 && targetTotalRaised >= targetCampaign.GoalAmount)
            {
                return (false, "Target campaign has already reached its fundraising goal.");
            }

            // Record surplus transaction
            var surplusTransaction = new CampaignSurplusTransaction
            {
                SourceCampaignId = sourceCampaignId,
                TargetCampaignId = targetCampaignId,
                TransactionType = "FundAllocated",
                Amount = amount,
                TransactionDate = DateTime.UtcNow,
                Notes = notes ?? $"Surplus funds allocated from '{sourceCampaign.Title}' to '{targetCampaign.Title}' by Admin #{adminId}."
            };

            _context.CampaignSurplusTransactions.Add(surplusTransaction);

            // If target campaign reaches goal, close it automatically
            var newTargetTotal = targetTotalRaised + amount;
            bool targetGoalReached = false;
            if (targetCampaign.GoalAmount > 0 && newTargetTotal >= targetCampaign.GoalAmount && targetCampaign.Status == "OPEN")
            {
                targetCampaign.Status = "CLOSED";
                targetCampaign.CloseReason = "GOAL_REACHED";
                targetCampaign.ClosedAt = DateTime.UtcNow;
                targetCampaign.UpdatedAt = DateTime.UtcNow;
                targetGoalReached = true;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _cache.InvalidateHomeCaches();

            // Send notifications to campaign owners
            try
            {
                await _notificationService.CreateAsync(
                    targetCampaign.UserId,
                    "Surplus Funds Received! 🎁",
                    $"Your campaign \"{targetCampaign.Title}\" has received an allocation of {amount:N0} MMK from community surplus funds!");

                await _notificationService.CreateAsync(
                    sourceCampaign.UserId,
                    "Surplus Funds Allocated 🤝",
                    $"{amount:N0} MMK from the surplus of your campaign \"{sourceCampaign.Title}\" has been allocated to support \"{targetCampaign.Title}\".");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create surplus notifications.");
            }

            // Real-time SignalR broadcasts
            try
            {
                var progressPercent = targetCampaign.GoalAmount > 0
                    ? (int)Math.Min(100, Math.Round(newTargetTotal / targetCampaign.GoalAmount * 100))
                    : 0;

                await _hubContext.Clients.All.SendAsync("CampaignDonationUpdated", new
                {
                    campaignId = targetCampaign.Id,
                    raisedAmount = newTargetTotal,
                    goalAmount = targetCampaign.GoalAmount,
                    progressPercent,
                    status = targetCampaign.Status
                });

                if (targetGoalReached)
                {
                    await _hubContext.Clients.All.SendAsync("CampaignStatusChanged", new
                    {
                        campaignId = targetCampaign.Id,
                        status = "CLOSED",
                        closeReason = "GOAL_REACHED",
                        title = targetCampaign.Title
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast surplus allocation SignalR update.");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to allocate surplus from campaign {SourceId} to {TargetId}", sourceCampaignId, targetCampaignId);
            return (false, "An error occurred while allocating surplus funds.");
        }
    }
}
