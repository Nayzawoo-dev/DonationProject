using Donation.Services;
using Donation.ViewModels.Donation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Donation.Controllers;

[Authorize]
public class DonationController : Controller
{
    private readonly DonationService _donationService;
    private readonly CampaignService _campaignService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DonationController> _logger;

    public DonationController(
        DonationService donationService,
        CampaignService campaignService,
        IConfiguration configuration,
        ILogger<DonationController> logger)
    {
        _donationService = donationService;
        _campaignService = campaignService;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }

    // GET: /Donation/Submit/5
    [HttpGet]
    public async Task<IActionResult> Submit(int id)
    {
        var userId = GetCurrentUserId();
        var campaign = await _campaignService.GetDetailAsync(id, userId);

        if (campaign == null) return NotFound();
        if (campaign.OwnerId == userId)
        {
            TempData["ErrorMessage"] = "You cannot donate to your own campaign.";
            return RedirectToAction("Detail", "Campaign", new { id });
        }
        if (campaign.Status != "OPEN")
        {
            TempData["ErrorMessage"] = "This campaign is not currently accepting donations.";
            return RedirectToAction("Detail", "Campaign", new { id });
        }

        var model = new DonationSubmitViewModel
        {
            CampaignId = id,
            CampaignTitle = campaign.Title,
            CampaignGoal = campaign.GoalAmount,
            CampaignRaised = campaign.RaisedAmount,
            PaymentMethod = _configuration["DonationPayment:Method"] ?? "KPay",
            PaymentPhone = _configuration["DonationPayment:PhoneNumber"] ?? "",
            AccountName = _configuration["DonationPayment:AccountName"] ?? ""
        };

        return View(model);
    }

    // POST: /Donation/Submit
    [Authorize(Roles = "USER")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(DonationSubmitViewModel model)
    {
        var userId = GetCurrentUserId();

        if (model.TransferScreenshot == null || model.TransferScreenshot.Length == 0)
            ModelState.AddModelError("TransferScreenshot", "Please upload a transfer screenshot.");

        if (!ModelState.IsValid)
        {
            // Re-populate payment info
            model.PaymentMethod = _configuration["DonationPayment:Method"] ?? "KPay";
            model.PaymentPhone = _configuration["DonationPayment:PhoneNumber"] ?? "";
            model.AccountName = _configuration["DonationPayment:AccountName"] ?? "";
            return View(model);
        }

        var (success, error) = await _donationService.SubmitDonationAsync(
            model.CampaignId, userId, model.TransferScreenshot!);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            model.PaymentMethod = _configuration["DonationPayment:Method"] ?? "KPay";
            model.PaymentPhone = _configuration["DonationPayment:PhoneNumber"] ?? "";
            model.AccountName = _configuration["DonationPayment:AccountName"] ?? "";
            return View(model);
        }

        TempData["SuccessMessage"] = "Your donation has been submitted! Our team will verify your transfer and update the status.";
        return RedirectToAction("My");
    }

    // GET: /Donation/My
    [HttpGet]
    public async Task<IActionResult> My()
    {
        var userId = GetCurrentUserId();
        var vm = await _donationService.GetMyDonationsAsync(userId);
        return View(vm);
    }
}
