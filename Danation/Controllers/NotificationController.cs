using Donation.Services;
using Donation.ViewModels.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Donation.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly NotificationService _notificationService;

    public NotificationController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }

    // GET: /Notification — All notifications page
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetAllAsync(userId);
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        return View(new NotificationListViewModel { Notifications = notifications, UnreadCount = unreadCount });
    }

    // GET: /Notification/UnreadCount (AJAX)
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Json(new { count });
    }

    // GET: /Notification/Latest (AJAX)
    [HttpGet]
    public async Task<IActionResult> Latest()
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetLatestAsync(userId, 8);
        return Json(new { success = true, data = notifications });
    }

    // POST: /Notification/MarkRead/5 (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = GetCurrentUserId();
        var success = await _notificationService.MarkReadAsync(id, userId);
        return Json(new { success, message = success ? "Marked as read." : "Notification not found." });
    }

    // POST: /Notification/MarkAllRead (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.MarkAllReadAsync(userId);
        return Json(new { success = true, message = $"{count} notification(s) marked as read.", count });
    }
}
