using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Host)]
public class HostController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public HostController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage files,
        IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _files = files;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Dashboard()
    {
        var uid = CurrentUserId;
        var user = await _userManager.FindByIdAsync(uid);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var mine = _db.Bookings.Where(b => b.Property!.HostId == uid);
        var paid = _db.Payments.Where(p => p.Booking!.Property!.HostId == uid && p.Status == PaymentStatus.Paid);

        var decided = await mine.CountAsync(b => b.Status != BookingStatus.Pending && b.Status != BookingStatus.Cancelled);
        var accepted = await mine.CountAsync(b => b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed);
        var paidCount = await paid.CountAsync();
        var totalEarnings = await paid.SumAsync(p => (decimal?)(p.Amount - p.Booking!.ServiceFee)) ?? 0m;

        var vm = new HostDashboardViewModel
        {
            HostName = user?.FullName ?? string.Empty,
            TotalProperties = await _db.HostProperties.CountAsync(p => p.HostId == uid),
            ActiveProperties = await _db.HostProperties.CountAsync(p => p.HostId == uid && p.IsActive),
            TotalBookings = await mine.CountAsync(),
            BookingsThisMonth = await mine.CountAsync(b => b.CreatedAt >= monthStart),
            PendingRequests = await mine.CountAsync(b => b.Status == BookingStatus.Pending),
            PendingCheckIns = await _db.GuestCheckIns.CountAsync(c => c.Booking!.Property!.HostId == uid && c.Status == CheckInStatus.Submitted),
            TotalEarnings = totalEarnings,
            EarningsThisMonth = await paid.Where(p => p.CreatedAt >= monthStart).SumAsync(p => (decimal?)(p.Amount - p.Booking!.ServiceFee)) ?? 0m,
            AverageRating = await _db.Reviews.Where(r => r.Property!.HostId == uid).AverageAsync(r => (double?)r.Rating) ?? 0,
            ReviewCount = await _db.Reviews.CountAsync(r => r.Property!.HostId == uid),
            AcceptanceRate = decided == 0 ? 0 : Math.Round(accepted * 100.0 / decided, 1),
            AverageBookingValue = paidCount == 0 ? 0 : Math.Round(totalEarnings / paidCount, 2),
            RecentBookings = await mine.AsNoTracking().Include(b => b.Property).Include(b => b.Guest)
                .OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    public async Task<IActionResult> Properties()
    {
        var uid = CurrentUserId;
        var list = await _db.HostProperties.AsNoTracking().AsSplitQuery()
            .Include(p => p.Images).Include(p => p.Reviews).Include(p => p.Bookings)
            .Where(p => p.HostId == uid).OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var uid = CurrentUserId;
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == uid);
        if (property == null) return NotFound();

        if (!property.IsApproved)
        {
            Error("This listing is still awaiting admin approval and cannot be activated yet.");
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(uid, property.IsActive ? "PropertyActivated" : "PropertyDeactivated", ActivityType.Property, property.Title);
        Success($"'{property.Title}' is now {(property.IsActive ? "active" : "inactive")}.");
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> EditProperty(int id)
    {
        var uid = CurrentUserId;
        var p = await _db.HostProperties.Include(x => x.Images).Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == id && x.HostId == uid);
        if (p == null) return NotFound();
        return View(await ToForm(p));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProperty(PropertyFormViewModel model)
    {
        var uid = CurrentUserId;
        var p = await _db.HostProperties.Include(x => x.Images).Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == model.Id && x.HostId == uid);
        if (p == null) return NotFound();

        var newFiles = (model.Images ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
        foreach (var f in newFiles)
        {
            var err = _files.Validate(f, isDocument: false);
            if (err != null) ModelState.AddModelError(nameof(model.Images), err);
        }
        if (!ModelState.IsValid)
        {
            model.AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
            model.ExistingImages = p.Images.ToList();
            return View(model);
        }

        p.Title = model.Title.Trim(); p.Description = model.Description.Trim();
        p.PricePerNight = model.PricePerNight; p.CleaningFee = model.CleaningFee;
        p.Address = model.Address.Trim(); p.City = model.City.Trim(); p.Country = model.Country.Trim();
        p.PropertyType = model.PropertyType; p.MaxGuests = model.MaxGuests;
        p.Bedrooms = model.Bedrooms; p.Beds = model.Beds; p.Bathrooms = model.Bathrooms;

        p.Amenities.Clear();
        foreach (var a in await _db.Amenities.Where(a => model.SelectedAmenityIds.Contains(a.Id)).ToListAsync())
            p.Amenities.Add(a);

        foreach (var f in newFiles)
            p.Images.Add(new PropertyImage { ImageUrl = await _files.SaveImageAsync(f) });

        await _db.SaveChangesAsync();
        await _logger.LogAsync(uid, "PropertyUpdated", ActivityType.Property, p.Title);
        Success("Listing updated.");
        return RedirectToAction(nameof(Properties));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var uid = CurrentUserId;
        var image = await _db.PropertyImages.Include(i => i.Property)
            .FirstOrDefaultAsync(i => i.Id == imageId && i.Property!.HostId == uid);
        if (image == null) return NotFound();

        var count = await _db.PropertyImages.CountAsync(i => i.PropertyId == image.PropertyId);
        if (count <= 1)
        {
            Error("A listing must keep at least one image.");
        }
        else
        {
            _files.DeleteImage(image.ImageUrl);
            _db.PropertyImages.Remove(image);
            await _db.SaveChangesAsync();
            Success("Image removed.");
        }
        return RedirectToAction(nameof(EditProperty), new { id = image.PropertyId });
    }

    public async Task<IActionResult> Bookings(string? status)
    {
        var uid = CurrentUserId;
        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Property).Include(b => b.Guest).Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .Where(b => b.Property!.HostId == uid);

        if (Enum.TryParse<BookingStatus>(status, out var parsed)) query = query.Where(b => b.Status == parsed);

        ViewBag.Status = status;
        var list = await query.OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1).ThenByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var uid = CurrentUserId;
        var booking = await _db.Bookings.Include(b => b.Property).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property!.HostId == uid);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            Error("Only pending requests can be approved.");
            return RedirectToAction(nameof(Bookings));
        }

        var clash = await _db.Bookings.AnyAsync(b => b.PropertyId == booking.PropertyId && b.Id != booking.Id
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
            && b.CheckInDate < booking.CheckOutDate && b.CheckOutDate > booking.CheckInDate);
        if (clash)
        {
            Error("These dates overlap with another approved booking.");
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Approved;
        // no real payment gateway: approving a booking counts as the payment going through
        if (booking.Payment != null) booking.Payment.Status = PaymentStatus.Paid;
        await _db.SaveChangesAsync();

        await _logger.LogAsync(uid, "BookingApproved", ActivityType.Booking, $"Booking #{id}: Pending -> Approved");
        await _notifications.NotifyAsync(booking.GuestId, "Booking Approved",
            $"Your booking for '{booking.Property!.Title}' was approved. Complete your check-in before you arrive.", NotificationType.Success, $"/Bookings/Details/{id}");

        Success("Booking approved.");
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id)
    {
        var uid = CurrentUserId;
        var booking = await _db.Bookings.Include(b => b.Property).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property!.HostId == uid);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            Error("Only pending requests can be rejected.");
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Rejected;
        await _db.SaveChangesAsync();

        await _logger.LogAsync(uid, "BookingRejected", ActivityType.Booking, $"Booking #{id}: Pending -> Rejected");
        await _notifications.NotifyAsync(booking.GuestId, "Booking Declined",
            $"Unfortunately your request for '{booking.Property!.Title}' was declined.", NotificationType.Warning, $"/Bookings/Details/{id}");

        Success("Booking rejected.");
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteStay(int id)
    {
        var uid = CurrentUserId;
        var booking = await _db.Bookings.Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property!.HostId == uid);
        if (booking == null) return NotFound();

        if (booking.Status == BookingStatus.CheckedIn)
        {
            booking.Status = BookingStatus.Completed;
            await _db.SaveChangesAsync();
            await _logger.LogAsync(uid, "BookingCompleted", ActivityType.Booking, $"Booking #{id}: CheckedIn -> Completed");
            await _notifications.NotifyAsync(booking.GuestId, "Stay Completed", $"Thanks for staying at '{booking.Property!.Title}'. Leave a review!", NotificationType.Info, $"/Bookings/Details/{id}");
            Success("Stay marked as completed.");
        }
        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> CheckIns()
    {
        var uid = CurrentUserId;
        var list = await _db.GuestCheckIns.AsNoTracking()
            .Include(c => c.Booking).ThenInclude(b => b!.Property)
            .Include(c => c.Booking).ThenInclude(b => b!.Guest)
            .Include(c => c.Documents)
            .Where(c => c.Booking!.Property!.HostId == uid)
            .OrderBy(c => c.Status == CheckInStatus.Submitted ? 0 : 1).ThenByDescending(c => c.CreatedAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> CheckInReview(int id)
    {
        var checkIn = await LoadCheckIn(id);
        if (checkIn == null) return NotFound();
        return View(checkIn);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int documentId)
    {
        var doc = await LoadDocument(documentId);
        if (doc == null) return NotFound();
        doc.Status = DocumentStatus.Verified;
        doc.RejectionReason = null;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(CheckInReview), new { id = doc.GuestCheckInId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDocument(int documentId, string? reason)
    {
        var doc = await LoadDocument(documentId);
        if (doc == null) return NotFound();
        var checkIn = doc.GuestCheckIn!;

        doc.Status = DocumentStatus.Rejected;
        doc.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Document not acceptable" : reason.Trim();
        checkIn.Status = CheckInStatus.Rejected;
        checkIn.CurrentStep = 2;
        await _db.SaveChangesAsync();

        await _logger.LogAsync(CurrentUserId, "GuestDocumentRejected", ActivityType.CheckIn, $"Document #{documentId}");
        await _notifications.NotifyAsync(checkIn.Booking!.GuestId, "Check-in Document Rejected",
            $"Your {doc.DocumentType} was rejected: {doc.RejectionReason}. Please upload a new one.", NotificationType.Warning, $"/CheckIn?bookingId={checkIn.BookingId}");

        Error("Document rejected. The guest has been asked to upload a new one.");
        return RedirectToAction(nameof(CheckInReview), new { id = checkIn.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCheckIn(int id)
    {
        var checkIn = await LoadCheckIn(id);
        if (checkIn == null) return NotFound();

        var docs = checkIn.Documents.Where(d => d.Status != DocumentStatus.Rejected).ToList();
        if (checkIn.Status != CheckInStatus.Submitted || docs.Count == 0 || docs.Any(d => d.Status != DocumentStatus.Verified))
        {
            Error("Verify every submitted document before confirming the check-in.");
            return RedirectToAction(nameof(CheckInReview), new { id });
        }

        checkIn.Status = CheckInStatus.Verified;
        checkIn.VerifiedAt = DateTime.Now;
        checkIn.Booking!.Status = BookingStatus.CheckedIn;
        await _db.SaveChangesAsync();

        await _logger.LogAsync(CurrentUserId, "GuestCheckedIn", ActivityType.Booking, $"Booking #{checkIn.BookingId}: Approved -> CheckedIn");
        await _notifications.NotifyAsync(checkIn.Booking.GuestId, "Check-in Confirmed",
            $"Your documents were verified. Enjoy your stay at '{checkIn.Booking.Property!.Title}'!", NotificationType.Success, $"/Bookings/Details/{checkIn.BookingId}");

        Success("Guest checked in.");
        return RedirectToAction(nameof(CheckIns));
    }

    [HttpGet]
    public async Task<IActionResult> ConfigureCheckIn(int propertyId)
    {
        var uid = CurrentUserId;
        var property = await _db.HostProperties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == uid);
        if (property == null) return NotFound();

        var process = await _db.CheckInProcesses.AsNoTracking().FirstOrDefaultAsync(c => c.PropertyId == propertyId);
        var cfg = CheckInConfig.Parse(process?.StepsJson);
        return View(new CheckInConfigViewModel
        {
            PropertyId = propertyId, PropertyTitle = property.Title,
            Title = process?.Title ?? $"Check-in for {property.Title}",
            Instructions = cfg.Instructions,
            RulesText = string.Join(Environment.NewLine, cfg.Rules),
            DocumentsText = string.Join(Environment.NewLine, cfg.RequiredDocuments)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureCheckIn(CheckInConfigViewModel model)
    {
        var uid = CurrentUserId;
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == uid);
        if (property == null) return NotFound();
        model.PropertyTitle = property.Title;

        static List<string> Lines(string? text) => (text ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var docs = Lines(model.DocumentsText);
        if (docs.Count == 0) ModelState.AddModelError(nameof(model.DocumentsText), "Require at least one document type (e.g. South African ID).");
        if (!ModelState.IsValid) return View(model);

        var cfg = new CheckInConfig { Instructions = model.Instructions?.Trim() ?? string.Empty, Rules = Lines(model.RulesText), RequiredDocuments = docs };

        var process = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == model.PropertyId);
        if (process == null)
        {
            process = new CheckInProcess { PropertyId = model.PropertyId };
            _db.CheckInProcesses.Add(process);
        }
        process.Title = model.Title.Trim();
        process.StepsJson = cfg.ToJson();
        await _db.SaveChangesAsync();

        await _logger.LogAsync(uid, "CheckInProcessConfigured", ActivityType.Property, property.Title);
        Success("Check-in process saved.");
        return RedirectToAction(nameof(Properties));
    }

    public async Task<IActionResult> Earnings()
    {
        var uid = CurrentUserId;
        var payments = await _db.Payments.AsNoTracking()
            .Include(p => p.Booking).ThenInclude(b => b!.Property)
            .Include(p => p.Booking).ThenInclude(b => b!.Guest)
            .Where(p => p.Booking!.Property!.HostId == uid && p.Status == PaymentStatus.Paid)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(payments);
    }

    public async Task<IActionResult> Reviews()
    {
        var uid = CurrentUserId;
        var reviews = await _db.Reviews.AsNoTracking()
            .Include(r => r.Property).Include(r => r.Reviewer)
            .Where(r => r.Property!.HostId == uid).OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
    }

    private async Task<GuestCheckIn?> LoadCheckIn(int id)
    {
        var uid = CurrentUserId;
        return await _db.GuestCheckIns.AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Booking).ThenInclude(b => b!.Property)
            .Include(c => c.Booking).ThenInclude(b => b!.Guest)
            .FirstOrDefaultAsync(c => c.Id == id && c.Booking!.Property!.HostId == uid);
    }

    private async Task<GuestDocument?> LoadDocument(int documentId)
    {
        var uid = CurrentUserId;
        return await _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking).ThenInclude(b => b!.Property)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.GuestCheckIn!.Booking!.Property!.HostId == uid);
    }

    private async Task<PropertyFormViewModel> ToForm(HostProperty p) => new()
    {
        Id = p.Id, Title = p.Title, Description = p.Description, PricePerNight = p.PricePerNight, CleaningFee = p.CleaningFee,
        Address = p.Address, City = p.City, Country = p.Country, PropertyType = p.PropertyType, MaxGuests = p.MaxGuests,
        Bedrooms = p.Bedrooms, Beds = p.Beds, Bathrooms = p.Bathrooms,
        SelectedAmenityIds = p.Amenities.Select(a => a.Id).ToList(),
        AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync(),
        ExistingImages = p.Images.ToList()
    };
}
