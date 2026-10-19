using Staybnb.Data;
using Staybnb.Helpers;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IActivityLogger
{
    Task LogAsync(string userId, string action, ActivityType type, string? details = null);
}

public class ActivityLogger : IActivityLogger
{
    private readonly ApplicationDbContext _db;

    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(string userId, string action, ActivityType type, string? details = null)
    {
        if (string.IsNullOrEmpty(userId)) return;

        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action.Clip(100),
            ActivityType = type,
            Details = details?.Clip(500),
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}
