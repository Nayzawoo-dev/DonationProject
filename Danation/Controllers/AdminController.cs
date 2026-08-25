using DatabaseClass.Models;
using Donation.Services;
using Donation.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Donation.Controllers;

[Authorize(Roles = "ADMIN")]
[EnableRateLimiting("RoleBasedPolicy")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly CampaignService _campaignService;
    private readonly DonationService _donationService;
    private readonly FileService _fileService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext context,
        CampaignService campaignService,
        DonationService donationService,
        FileService fileService,
        NotificationService notificationService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _campaignService = campaignService;
        _donationService = donationService;
        _fileService = fileService;
        _notificationService = notificationService;
        _logger = logger;
    }

    private int GetCurrentAdminId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }

    // GET: /Admin/Dashboard
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await _context.Users.AsNoTracking().CountAsync(u => u.Role == "USER"),
            TotalCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(),
            PendingCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "PENDING"),
            OpenCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "OPEN"),
            GoalReachedCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "GOAL_REACHED"),
            ClosedCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "CLOSED"),
            CompletedCampaigns = await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "COMPLETED"),
            PendingDonations = await _context.Donations.AsNoTracking().CountAsync(d => d.Status == "PENDING"),
            ApprovedDonations = await _context.Donations.AsNoTracking().CountAsync(d => d.Status == "APPROVED"),
            RejectedDonations = await _context.Donations.AsNoTracking().CountAsync(d => d.Status == "REJECTED"),
            TotalApprovedAmount = await _context.Donations.AsNoTracking()
                .Where(d => d.Status == "APPROVED")
                .SumAsync(d => (decimal?)d.Amount) ?? 0
        };

        // Recent pending campaigns (top 5)
        vm.RecentPendingCampaigns = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == "PENDING")
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new AdminCampaignSummaryViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Status = c.Status,
                GoalAmount = c.GoalAmount,
                OwnerName = c.User.FullName,
                OwnerUsername = c.User.Username,
                CreatedAt = c.CreatedAt,
                DocumentCount = c.CampaignDocuments.Count,
                ImageCount = c.CampaignImages.Count
            })
            .ToListAsync();

        // Recent pending donations (top 5)
        vm.RecentPendingDonations = await _context.Donations
            .AsNoTracking()
            .Where(d => d.Status == "PENDING")
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new AdminDonationSummaryViewModel
            {
                Id = d.Id,
                CampaignId = d.CampaignId,
                CampaignTitle = d.Campaign.Title,
                DonorName = d.Donor.FullName,
                DonorEmail = d.Donor.Email,
                TransferScreenshot = d.TransferScreenshot,
                Status = d.Status,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return View(vm);
    }

    // GET: /Admin/Campaigns
    [HttpGet]
    public async Task<IActionResult> Campaigns(string? status, string? search)
    {
        var campaigns = await _campaignService.GetAdminCampaignsAsync(status, search);
        ViewBag.StatusFilter = status;
        ViewBag.Search = search;
        return View(campaigns);
    }

    // POST: /Admin/ApproveCampaign (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCampaign(int id)
    {
        var adminId = GetCurrentAdminId();
        var (success, error) = await _campaignService.ApproveCampaignAsync(id, adminId);
        return Json(new { success, message = success ? "Campaign approved and is now OPEN for donations." : error });
    }

    // POST: /Admin/RejectCampaign (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCampaign(int id, [FromForm] string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Json(new { success = false, message = "A reason for rejection is required." });

        var adminId = GetCurrentAdminId();
        var (success, error) = await _campaignService.RejectCampaignAsync(id, adminId, reason);
        return Json(new { success, message = success ? "Campaign has been rejected." : error });
    }

    // POST: /Admin/CloseCampaign (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseCampaign(int id)
    {
        var adminId = GetCurrentAdminId();
        var (success, error) = await _campaignService.CloseCampaignAsync(id, adminId);
        return Json(new { success, message = success ? "Campaign has been closed." : error });
    }

    // GET: /Admin/Donations
    [HttpGet]
    public async Task<IActionResult> Donations(string? status)
    {
        var donations = await _donationService.GetAdminDonationsAsync(status);
        ViewBag.StatusFilter = status;
        return View(donations);
    }

    // POST: /Admin/ApproveDonation (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDonation(int id, [FromForm] decimal amount)
    {
        if (amount <= 0)
            return Json(new { success = false, message = "Please enter a valid verified amount." });

        var adminId = GetCurrentAdminId();
        var (success, error) = await _donationService.ApproveDonationAsync(id, amount, adminId);
        return Json(new { success, message = success ? "Donation approved successfully." : error });
    }

    // POST: /Admin/RejectDonation (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDonation(int id, [FromForm] string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Json(new { success = false, message = "A reason for rejection is required." });

        var adminId = GetCurrentAdminId();
        var (success, error) = await _donationService.RejectDonationAsync(id, adminId, reason);
        return Json(new { success, message = success ? "Donation has been rejected." : error });
    }

    // GET: /Admin/CreateCompletion/5
    [HttpGet]
    public async Task<IActionResult> CreateCompletion(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null) return NotFound();
        if (campaign.Status != "CLOSED")
        {
            TempData["ErrorMessage"] = "Only CLOSED campaigns can be marked as completed.";
            return RedirectToAction(nameof(Campaigns));
        }
        // Check no existing completion
        var existing = await _context.CampaignCompletions.AnyAsync(c => c.CampaignId == id);
        if (existing)
        {
            TempData["ErrorMessage"] = "This campaign already has a completion record.";
            return RedirectToAction(nameof(Campaigns));
        }

        var vm = new AdminCreateCompletionViewModel
        {
            CampaignId = id,
            CampaignTitle = campaign.Title
        };
        return View(vm);
    }

    // POST: /Admin/CreateCompletion
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompletion(AdminCreateCompletionViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var campaign = await _context.Campaigns.FindAsync(model.CampaignId);
        if (campaign == null || campaign.Status != "CLOSED")
            return NotFound();

        var adminId = GetCurrentAdminId();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var completion = new CampaignCompletion
            {
                CampaignId = model.CampaignId,
                Caption = model.Caption,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow
            };
            _context.CampaignCompletions.Add(completion);
            await _context.SaveChangesAsync();

            // Upload completion images
            if (model.CompletionImages != null && model.CompletionImages.Any())
            {
                foreach (var imageFile in model.CompletionImages)
                {
                    var (valid, error) = _fileService.ValidateImageFile(imageFile);
                    if (!valid) continue;

                    var imageUrl = await _fileService.SaveImageAsync(imageFile, "completions");
                    _context.CompletionImages.Add(new CompletionImage
                    {
                        CompletionId = completion.Id,
                        ImageUrl = imageUrl,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
            }

            // Update campaign status
            campaign.Status = "COMPLETED";
            campaign.CompletedAt = DateTime.UtcNow;
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Notify campaign owner
            await _notificationService.CreateAsync(
                campaign.UserId,
                "Campaign Completed! 🏆",
                $"Your campaign \"{campaign.Title}\" has been officially completed. Thank you for your incredible contribution!");

            TempData["SuccessMessage"] = "Campaign completion record created successfully.";
            return RedirectToAction(nameof(Campaigns));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create completion for campaign {CampaignId}", model.CampaignId);
            ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
            return View(model);
        }
    }

    // GET: /Admin/Users
    [HttpGet]
    public async Task<IActionResult> Users(string? search)
    {
        var query = _context.Users.AsNoTracking().Where(u => u.Role == "USER");
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search) || u.Username.Contains(search));

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role,
                EmailVerified = u.EmailVerified,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                TotalCampaigns = u.Campaigns.Count,
                TotalDonations = u.DonationDonors.Count
            })
            .ToListAsync();

        ViewBag.Search = search;
        return View(users);
    }

    // POST: /Admin/ToggleUserActive (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserActive(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.Role == "ADMIN")
            return Json(new { success = false, message = "User not found or cannot modify admin accounts." });

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, isActive = user.IsActive, message = user.IsActive ? "User activated." : "User deactivated." });
    }

    // GET: /Admin/CampaignDetail/5 — View campaign docs + contact phone
    [HttpGet]
    public async Task<IActionResult> CampaignDetail(int id)
    {
        var campaign = await _campaignService.GetDetailWithDocsAsync(id, 0);
        if (campaign == null) return NotFound();

        var raw = await _context.Campaigns.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.ContactPhone }).FirstOrDefaultAsync();
        ViewBag.ContactPhone = raw?.ContactPhone;

        return View(campaign);
    }
}
