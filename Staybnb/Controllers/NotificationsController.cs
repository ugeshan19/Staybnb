using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController : BaseController
{
    private readonly ApplicationDbContext _db;

    public NotificationsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? filter)
    {
        var userId = CurrentUserId;
        var mine = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        ViewBag.Filter = filter ?? "all";
        ViewBag.AllCount = await mine.CountAsync();
        ViewBag.UnreadCount = await mine.CountAsync(n => !n.IsRead);

        var shown = mine;
        if (filter == "unread") shown = mine.Where(n => !n.IsRead);
        else if (filter == "read") shown = mine.Where(n => n.IsRead);

        return View(await shown.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());
    }

    // marks the notification as read and goes to its link
    public async Task<IActionResult> Open(int id)
    {
        var note = await FindOwned(id);
        if (note == null) return NotFound();

        if (!note.IsRead)
        {
            note.IsRead = true;
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(note.Link) && Url.IsLocalUrl(note.Link))
            return LocalRedirect(note.Link);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var note = await FindOwned(id);
        if (note != null)
        {
            note.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId;
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
            n.IsRead = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await FindOwned(id);
        if (note != null)
        {
            _db.Notifications.Remove(note);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private Task<Notification?> FindOwned(int id)
    {
        var userId = CurrentUserId;
        return _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }
}
