using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Public listing pages. Hosts and admins get sent to their dashboards instead.
public class PropertiesController : BaseController
{
    private readonly ApplicationDbContext _db;

    public PropertiesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? city, PropertyType? type)
    {
        var redirect = StaffRedirect();
        if (redirect != null) return redirect;

        var query = _db.HostProperties.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Where(p => p.IsActive && p.IsApproved);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City.Contains(city.Trim()));
        if (type.HasValue)
            query = query.Where(p => p.PropertyType == type.Value);

        var model = new PropertyIndexViewModel
        {
            City = city,
            Type = type,
            Properties = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(),
            Cities = await _db.HostProperties
                .Where(p => p.IsActive && p.IsApproved)
                .Select(p => p.City).Distinct().OrderBy(c => c)
                .ToListAsync()
        };

        if (User.IsInRole(Roles.Guest))
        {
            var userId = CurrentUserId;
            var saved = await _db.WishlistItems.Where(w => w.UserId == userId).Select(w => w.PropertyId).ToListAsync();
            model.WishlistIds = saved.ToHashSet();
        }
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var redirect = StaffRedirect();
        if (redirect != null) return redirect;

        var property = await _db.HostProperties.AsNoTracking().AsSplitQuery()
            .Include(p => p.Images)
            .Include(p => p.Amenities)
            .Include(p => p.Host)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.IsApproved);
        if (property == null) return NotFound();

        var isGuest = User.IsInRole(Roles.Guest);
        var model = new PropertyDetailsViewModel
        {
            Property = property,
            IsGuest = isGuest,
            Booking = new BookingRequestViewModel { PropertyId = id }
        };

        if (isGuest)
        {
            var userId = CurrentUserId;
            model.IsWishlisted = await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.PropertyId == id);
        }
        return View(model);
    }

    // used by the booking form (ajax) to show the price breakdown
    [HttpGet]
    public async Task<IActionResult> Quote(int propertyId, DateTime checkIn, DateTime checkOut)
    {
        var property = await _db.HostProperties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId);
        if (property == null) return NotFound();

        if (checkOut.Date <= checkIn.Date)
            return Json(new { valid = false, message = "Check-out must be after check-in." });

        var q = BookingPricing.Calculate(property.PricePerNight, property.CleaningFee, checkIn, checkOut);
        return Json(new
        {
            valid = true,
            nights = q.Nights,
            nightlyRate = q.NightlyRate,
            subtotal = q.Subtotal,
            cleaningFee = q.CleaningFee,
            serviceFee = q.ServiceFee,
            total = q.Total
        });
    }

    private IActionResult? StaffRedirect()
    {
        if (User.IsInRole(Roles.Host))
            return RedirectToAction("Dashboard", "Host");
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin))
            return RedirectToAction("Dashboard", "Admin");
        return null;
    }
}
