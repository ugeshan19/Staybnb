using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.ViewModels;

namespace Staybnb.ViewComponents;

// Bell icon + envelope badges shown in every layout.
public class TopBarViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public TopBarViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var vm = new TopBarViewModel();
        var uid = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(uid))
        {
            vm.UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
            vm.UnreadMessages = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead);
            vm.Recent = await _db.Notifications.AsNoTracking().Where(n => n.UserId == uid)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync();
        }
        return View(vm);
    }
}
