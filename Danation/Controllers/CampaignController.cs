using Donation.Services;
using Donation.ViewModels.Campaign;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Donation.Controllers;

public class CampaignController : Controller
{
    private readonly CampaignService _campaignService;
    private readonly ILogger<CampaignController> _logger;

    public CampaignController(CampaignService campaignService, ILogger<CampaignController> logger)
    {
        _campaignService = campaignService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }

    // GET: /Campaign — Public campaign listing
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var vm = await _campaignService.GetPublicCampaignsAsync(search, status, page);
        return View(vm);
    }

    // GET: /Campaign/Detail/5 — Public campaign detail
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        int? currentUserId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;
        var campaign = await _campaignService.GetDetailAsync(id, currentUserId);
        if (campaign == null) return NotFound();

        // Only show non-public statuses to owner/admin
        if (campaign.Status == "PENDING" || campaign.Status == "REJECTED")
        {
            if (currentUserId == null || (campaign.OwnerId != currentUserId && !User.IsInRole("ADMIN")))
                return NotFound();
        }

        return View(campaign);
    }

    // GET: /Campaign/My — User's own campaigns
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> My()
    {
        var userId = GetCurrentUserId();
        var campaigns = await _campaignService.GetUserCampaignsAsync(userId);
        return View(new MyCampaignsViewModel { Campaigns = campaigns });
    }

    // GET: /Campaign/Create
    [Authorize]
    [HttpGet]
    public IActionResult Create() => View();

    // POST: /Campaign/Create
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCampaignViewModel model)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage });

        var userId = GetCurrentUserId();
        var (success, error, campaignId) = await _campaignService.CreateAsync(model, userId);

        if (!success)
            return Json(new { success = false, message = error });

        return Json(new { success = true, message = "Campaign created successfully! It is pending admin approval.", redirectUrl = Url.Action("Edit", new { id = campaignId }) });
    }

    // GET: /Campaign/Edit/5
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();
        var model = await _campaignService.GetEditViewModelAsync(id, userId);
        if (model == null) return NotFound();
        return View(model);
    }

    // POST: /Campaign/Edit/5
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCampaignViewModel model)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage });

        var userId = GetCurrentUserId();
        var (success, error) = await _campaignService.UpdateAsync(model, userId);

        return Json(new { success, message = success ? "Campaign updated successfully!" : error });
    }

    // POST: /Campaign/Delete/5 (AJAX)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        var (success, error) = await _campaignService.DeleteAsync(id, userId);
        return Json(new { success, message = success ? "Campaign deleted successfully." : error });
    }

    // POST: /Campaign/UploadImage (AJAX)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(int campaignId, IFormFile file, string? caption)
    {
        var userId = GetCurrentUserId();
        var (success, error, image) = await _campaignService.UploadImageAsync(campaignId, file, caption, userId);

        if (!success)
            return Json(new { success = false, message = error });

        return Json(new { success = true, message = "Image uploaded successfully.", data = image });
    }

    // POST: /Campaign/DeleteImage (AJAX)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var userId = GetCurrentUserId();
        var (success, error) = await _campaignService.DeleteImageAsync(imageId, userId);
        return Json(new { success, message = success ? "Image deleted." : error });
    }

    // POST: /Campaign/UploadDocument (AJAX)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(int campaignId, IFormFile file, string? documentType)
    {
        var userId = GetCurrentUserId();
        var (success, error, doc) = await _campaignService.UploadDocumentAsync(campaignId, file, documentType, userId);

        if (!success)
            return Json(new { success = false, message = error });

        return Json(new { success = true, message = "Document uploaded successfully.", data = doc });
    }

    // POST: /Campaign/DeleteDocument (AJAX)
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int documentId)
    {
        var userId = GetCurrentUserId();
        var (success, error) = await _campaignService.DeleteDocumentAsync(documentId, userId);
        return Json(new { success, message = success ? "Document deleted." : error });
    }
}
