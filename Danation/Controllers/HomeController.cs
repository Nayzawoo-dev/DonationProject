using DatabaseClass.Models;
using Donation.Models;
using Donation.Services;
using Donation.ViewModels.Campaign;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Donation.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly CampaignService _campaignService;

        public HomeController(AppDbContext context, CampaignService campaignService)
        {
            _context = context;
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            // Stats for hero section
            ViewBag.TotalCampaigns     = await _context.Campaigns.CountAsync();
            ViewBag.OpenCampaigns      = await _context.Campaigns.CountAsync(c => c.Status == "OPEN");
            ViewBag.CompletedCampaigns = await _context.Campaigns.CountAsync(c => c.Status == "COMPLETED");
            ViewBag.TotalUsers         = await _context.Users.CountAsync(u => u.Role == "USER" && u.IsActive == true);

            // Recent open campaigns (up to 6)
            var recent = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Status == "OPEN")
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .Select(c => new CampaignListItemViewModel
                {
                    Id          = c.Id,
                    Title       = c.Title,
                    Description = c.Description,
                    GoalAmount  = c.GoalAmount,
                    RaisedAmount = c.Donations
                        .Where(d => d.Status == "APPROVED")
                        .Sum(d => (decimal?)d.Amount) ?? 0,
                    Status           = c.Status,
                    CreatedAt        = c.CreatedAt,
                    OwnerName        = c.User.FullName,
                    OwnerProfileImage = c.User.ProfileImage,
                    ThumbnailImage   = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                    ImageCount       = c.CampaignImages.Count
                })
                .ToListAsync();

            ViewBag.RecentCampaigns = recent;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
