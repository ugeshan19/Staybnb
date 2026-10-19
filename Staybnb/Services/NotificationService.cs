using Microsoft.AspNetCore.Identity;
using Staybnb.Data;
using Staybnb.Helpers;
using Staybnb.Models;

namespace Staybnb.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, NotificationType type, string? link = null);
    Task NotifyAdminsAsync(string title, string message, NotificationType type, string? link = null);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task NotifyAsync(string userId, string title, string message, NotificationType type, string? link = null)
    {
        if (string.IsNullOrEmpty(userId)) return;

        _db.Notifications.Add(Build(userId, title, message, type, link));
        await _db.SaveChangesAsync();
    }

    public async Task NotifyAdminsAsync(string title, string message, NotificationType type, string? link = null)
    {
        var admins = (await _userManager.GetUsersInRoleAsync(Roles.Admin))
            .Concat(await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin))
            .DistinctBy(u => u.Id);

        foreach (var admin in admins)
            _db.Notifications.Add(Build(admin.Id, title, message, type, link));

        await _db.SaveChangesAsync();
    }

    private static Notification Build(string userId, string title, string message, NotificationType type, string? link) => new()
    {
        UserId = userId,
        Title = title.Clip(100),
        Message = message.Clip(500),
        Type = type,
        Link = link,
        CreatedAt = DateTime.Now
    };
}
