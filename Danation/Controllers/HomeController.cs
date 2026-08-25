using DatabaseClass.Models;
using Donation.Models;
using Donation.Services;
using Donation.ViewModels.Campaign;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Donation.Controllers;

[EnableRateLimiting("RoleBasedPolicy")]
public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly CampaignService _campaignService;
    private readonly IMemoryCache _cache;

    public HomeController(AppDbContext context, CampaignService campaignService, IMemoryCache cache)
    {
        _context = context;
        _campaignService = campaignService;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        // Cache public landing page stats for 2 minutes to reduce database queries
        const string statsCacheKey = "HOME_LANDING_STATS";
        if (!_cache.TryGetValue(statsCacheKey, out (int total, int open, int completed, int users) stats))
        {
            stats = (
                await _context.Campaigns.AsNoTracking().CountAsync(),
                await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "OPEN"),
                await _context.Campaigns.AsNoTracking().CountAsync(c => c.Status == "COMPLETED"),
                await _context.Users.AsNoTracking().CountAsync(u => u.Role == "USER" && u.IsActive == true)
            );
            _cache.Set(statsCacheKey, stats, TimeSpan.FromMinutes(2));
        }

        ViewBag.TotalCampaigns     = stats.total;
        ViewBag.OpenCampaigns      = stats.open;
        ViewBag.CompletedCampaigns = stats.completed;
        ViewBag.TotalUsers         = stats.users;

        // Cache recent open campaigns (up to 6) for 1 minute
        const string recentCacheKey = "HOME_RECENT_CAMPAIGNS";
        if (!_cache.TryGetValue(recentCacheKey, out List<CampaignListItemViewModel>? recent) || recent == null)
        {
            recent = await _context.Campaigns
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
                    OwnerId          = c.UserId,
                    OwnerName        = c.User.FullName,
                    OwnerProfileImage = c.User.ProfileImage,
                    ThumbnailImage   = c.CampaignImages.OrderBy(i => i.CreatedAt).Select(i => i.ImageUrl).FirstOrDefault(),
                    ImageCount       = c.CampaignImages.Count,
                    Township         = c.Township
                })
                .ToListAsync();

            _cache.Set(recentCacheKey, recent, TimeSpan.FromMinutes(1));
        }

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
