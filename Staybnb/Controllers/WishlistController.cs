using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class WishlistController : BaseController
{
    private readonly ApplicationDbContext _db;

    public WishlistController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        var items = await _db.WishlistItems.AsNoTracking()
            .Include(w => w.Property).ThenInclude(p => p!.Images)
            .Include(w => w.Property).ThenInclude(p => p!.Reviews)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int propertyId, string? returnUrl)
    {
        var userId = CurrentUserId;
        var saved = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.PropertyId == propertyId);

        if (saved != null)
        {
            _db.WishlistItems.Remove(saved);
        }
        else if (await _db.HostProperties.AnyAsync(p => p.Id == propertyId && p.IsActive && p.IsApproved))
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = userId, PropertyId = propertyId });
        }
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
