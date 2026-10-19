using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Plain database messaging (no live updates): an inbox, and one thread per conversation partner.
[Authorize]
public class MessagesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Inbox()
    {
        var uid = CurrentUserId;
        var messages = await _db.Messages.AsNoTracking().Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == uid || m.ReceiverId == uid)
            .OrderByDescending(m => m.Timestamp).ToListAsync();

        var items = messages.GroupBy(m => m.SenderId == uid ? m.ReceiverId : m.SenderId)
            .Select(g =>
            {
                var last = g.First();
                return new InboxItem
                {
                    OtherUser = last.SenderId == uid ? last.Receiver! : last.Sender!,
                    LastMessage = last,
                    Unread = g.Count(m => m.ReceiverId == uid && !m.IsRead)
                };
            }).ToList();
        return View(items);
    }

    public async Task<IActionResult> Thread(string id)
    {
        var uid = CurrentUserId;
        if (id == uid) return RedirectToAction(nameof(Inbox));
        var other = await _userManager.FindByIdAsync(id);
        if (other == null) return NotFound();

        var messages = await _db.Messages
            .Where(m => (m.SenderId == uid && m.ReceiverId == id) || (m.SenderId == id && m.ReceiverId == uid))
            .OrderBy(m => m.Timestamp).ToListAsync();

        var unread = messages.Where(m => m.ReceiverId == uid && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return View(new ThreadViewModel { OtherUser = other, Messages = messages });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string? content)
    {
        var uid = CurrentUserId;
        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null || receiverId == uid) return NotFound();

        content = content?.Trim();
        if (string.IsNullOrEmpty(content) || content.Length > 2000)
        {
            Error("Message must be between 1 and 2000 characters.");
            return RedirectToAction(nameof(Thread), new { id = receiverId });
        }

        _db.Messages.Add(new Message { SenderId = uid, ReceiverId = receiverId, Content = content, Timestamp = DateTime.Now });
        await _db.SaveChangesAsync();

        var sender = await _userManager.FindByIdAsync(uid);
        await _notifications.NotifyAsync(receiverId, "New Message", $"{sender?.FullName} sent you a message.", NotificationType.Message, $"/Messages/Thread/{uid}");
        await _logger.LogAsync(uid, "MessageSent", ActivityType.Message, $"To {receiver.Email}");
        return RedirectToAction(nameof(Thread), new { id = receiverId });
    }

    // lets a user open a thread with anyone, looked up by email
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string? email)
    {
        var user = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email.Trim());
        if (user == null || user.Id == CurrentUserId)
        {
            Error("No other user found with that email address.");
            return RedirectToAction(nameof(Inbox));
        }
        return RedirectToAction(nameof(Thread), new { id = user.Id });
    }
}
