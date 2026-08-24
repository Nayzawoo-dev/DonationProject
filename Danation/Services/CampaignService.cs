using DatabaseClass.Models;
using Donation.ViewModels.Admin;
using Donation.ViewModels.Campaign;
using Microsoft.EntityFrameworkCore;

namespace Donation.Services;

public class CampaignService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<CampaignService> _logger;
    private const int PageSize = 9;

    public CampaignService(
        AppDbContext context,
        FileService fileService,
        NotificationService notificationService,
        ILogger<CampaignService> logger)
    {
        _context = context;
        _fileService = fileService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CampaignListViewModel> GetPublicCampaignsAsync(string? search, string? status, int page = 1)
    {
        var query = _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == "OPEN" || c.Status == "GOAL_REACHED" || c.Status == "CLOSED" || c.Status == "COMPLETED")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / PageSize);

        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new CampaignListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                RaisedAmount = c.Donations
                    .Where(d => d.Status == "APPROVED")
                    .Sum(d => (decimal?)d.Amount) ?? 0,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                OwnerName = c.User.FullName,
                OwnerProfileImage = c.User.ProfileImage,
                ThumbnailImage = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                ImageCount = c.CampaignImages.Count
            })
            .ToListAsync();

        return new CampaignListViewModel
        {
            Campaigns = campaigns,
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

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
                RaisedAmount = c.Donations
                    .Where(d => d.Status == "APPROVED")
                    .Sum(d => (decimal?)d.Amount) ?? 0,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                ClosedAt = c.ClosedAt,
                CompletedAt = c.CompletedAt,
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

        campaign.IsOwner = currentUserId.HasValue && campaign.OwnerId == currentUserId.Value;
        campaign.CanDonate = currentUserId.HasValue
            && !campaign.IsOwner
            && campaign.Status == "OPEN";

        return campaign;
    }

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

    public async Task<(bool Success, string ErrorMessage, int CampaignId)> CreateAsync(ViewModels.Campaign.CreateCampaignViewModel model, int userId)
    {
        var campaign = new Campaign
        {
            UserId = userId,
            Title = model.Title,
            Description = model.Description,
            GoalAmount = model.GoalAmount,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        return (true, string.Empty, campaign.Id);
    }

    public async Task<EditCampaignViewModel?> GetEditViewModelAsync(int id, int currentUserId)
    {
        return await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == id && c.UserId == currentUserId)
            .Select(c => new EditCampaignViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                GoalAmount = c.GoalAmount,
                Status = c.Status,
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

    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(EditCampaignViewModel model, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == model.Id && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found or you do not have permission to edit it.");

        if (campaign.Status != "PENDING" && campaign.Status != "OPEN")
            return (false, "Only PENDING or OPEN campaigns can be edited.");

        campaign.Title = model.Title;
        campaign.Description = model.Description;
        campaign.GoalAmount = model.GoalAmount;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
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
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage, CampaignImageViewModel? Image)> UploadImageAsync(int campaignId, IFormFile file, string? caption, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found.", null);

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

        _fileService.DeleteFile(image.ImageUrl);
        _context.CampaignImages.Remove(image);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage, CampaignDocumentViewModel? Document)> UploadDocumentAsync(int campaignId, IFormFile file, string? documentType, int currentUserId)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.UserId == currentUserId);
        if (campaign == null)
            return (false, "Campaign not found.", null);

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
                RaisedAmount = c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                OwnerName = c.User.FullName,
                ThumbnailImage = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                ImageCount = c.CampaignImages.Count
            })
            .ToListAsync();
    }

    // Admin methods
    public async Task<(bool Success, string ErrorMessage)> ApproveCampaignAsync(int campaignId, int adminId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return (false, "Campaign not found.");
        if (campaign.Status != "PENDING") return (false, "Only PENDING campaigns can be approved.");

        campaign.Status = "OPEN";
        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Approved! 🎉",
            $"Your campaign \"{campaign.Title}\" has been approved and is now live for donations.");

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

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Not Approved",
            $"Your campaign \"{campaign.Title}\" was not approved. Reason: {reason}");

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> CloseCampaignAsync(int campaignId, int adminId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return (false, "Campaign not found.");
        if (campaign.Status != "GOAL_REACHED") return (false, "Only GOAL_REACHED campaigns can be closed.");

        campaign.Status = "CLOSED";
        campaign.ClosedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(
            campaign.UserId,
            "Campaign Closed",
            $"Your campaign \"{campaign.Title}\" has been officially closed after reaching its goal. Thank you for making a difference!");

        return (true, string.Empty);
    }

    public async Task<List<AdminCampaignSummaryViewModel>> GetAdminCampaignsAsync(string? status, string? search)
    {
        var query = _context.Campaigns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.Contains(search) || c.User.FullName.Contains(search));

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminCampaignSummaryViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Status = c.Status,
                GoalAmount = c.GoalAmount,
                RaisedAmount = c.Donations.Where(d => d.Status == "APPROVED").Sum(d => (decimal?)d.Amount) ?? 0,
                OwnerName = c.User.FullName,
                OwnerUsername = c.User.Username,
                CreatedAt = c.CreatedAt,
                DocumentCount = c.CampaignDocuments.Count,
                ImageCount = c.CampaignImages.Count
            })
            .ToListAsync();
    }

}
