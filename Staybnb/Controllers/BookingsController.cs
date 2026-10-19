using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Helpers;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class BookingsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public BookingsController(ApplicationDbContext db, IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        var bookings = await _db.Bookings.AsNoTracking()
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn)
            .Where(b => b.GuestId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(bookings);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = CurrentUserId;
        var booking = await _db.Bookings.AsNoTracking().AsSplitQuery()
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Property).ThenInclude(p => p!.Host)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        ViewBag.HasReview = await _db.Reviews.AnyAsync(r => r.BookingId == id);
        return View(booking);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingRequestViewModel model)
    {
        var userId = CurrentUserId;
        var property = await _db.HostProperties
            .FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.IsActive && p.IsApproved);
        if (property == null) return NotFound();

        var problem = await FindProblem(property, model, userId);
        if (problem != null)
        {
            Error(problem);
            return RedirectToAction("Details", "Properties", new { id = property.Id });
        }

        var quote = BookingPricing.Calculate(property.PricePerNight, property.CleaningFee, model.CheckInDate, model.CheckOutDate);
        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = userId,
            Guests = model.Guests,
            CheckInDate = model.CheckInDate.Date,
            CheckOutDate = model.CheckOutDate.Date,
            NightlyRate = quote.NightlyRate,
            CleaningFee = quote.CleaningFee,
            ServiceFee = quote.ServiceFee,
            TotalPrice = quote.Total,
            Status = BookingStatus.Pending,
            Payment = new Payment { Amount = quote.Total, Status = PaymentStatus.Pending }
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _logger.LogAsync(userId, "BookingRequested", ActivityType.Booking,
            $"Booking #{booking.Id} for '{property.Title}' ({quote.Nights} nights)");
        await _notifications.NotifyAsync(property.HostId, "New Booking Request",
            $"{User.Identity?.Name} requested '{property.Title}' for {quote.Nights} night(s).",
            NotificationType.Booking, "/Host/Bookings");
        await _notifications.NotifyAsync(userId, "Booking Requested",
            $"Your request for '{property.Title}' was sent to the host.",
            NotificationType.Booking, $"/Bookings/Details/{booking.Id}");

        Success("Booking request sent! You'll be notified when the host responds.");
        return RedirectToAction(nameof(Details), new { id = booking.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = CurrentUserId;
        var booking = await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Approved)
        {
            Error("Only pending or approved bookings can be cancelled.");
            return RedirectToAction(nameof(Details), new { id });
        }

        booking.Status = BookingStatus.Cancelled;
        if (booking.Payment != null && booking.Payment.Status == PaymentStatus.Paid)
            booking.Payment.Status = PaymentStatus.Refunded;
        await _db.SaveChangesAsync();

        await _logger.LogAsync(userId, "BookingCancelled", ActivityType.Booking, $"Booking #{id} cancelled by guest");
        await _notifications.NotifyAsync(booking.Property!.HostId, "Booking Cancelled",
            $"A guest cancelled their booking for '{booking.Property.Title}'.",
            NotificationType.Warning, "/Host/Bookings");

        Success("Your booking was cancelled.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int id, int rating, string? comment)
    {
        var userId = CurrentUserId;
        var booking = await _db.Bookings.Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.CheckedIn && booking.Status != BookingStatus.Completed)
            Error("You can review a stay after you have checked in.");
        else if (rating < 1 || rating > 5)
            Error("Please choose a rating between 1 and 5.");
        else if (await _db.Reviews.AnyAsync(r => r.BookingId == id))
            Error("You have already reviewed this stay.");
        else
        {
            _db.Reviews.Add(new Review
            {
                PropertyId = booking.PropertyId,
                ReviewerId = userId,
                BookingId = id,
                Rating = rating,
                Comment = (comment?.Trim() ?? string.Empty).Clip(1000)
            });
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(booking.Property!.HostId, "New Review",
                $"A guest left a {rating}-star review for '{booking.Property.Title}'.",
                NotificationType.Info, "/Host/Reviews");
            Success("Thanks for your review!");
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // returns an error message, or null if the booking is fine
    private async Task<string?> FindProblem(HostProperty property, BookingRequestViewModel model, string userId)
    {
        if (model.CheckInDate.Date < DateTime.Today)
            return "Check-in cannot be in the past.";
        if (model.CheckOutDate.Date <= model.CheckInDate.Date)
            return "Check-out must be after check-in.";
        if (model.Guests < 1 || model.Guests > property.MaxGuests)
            return $"This property allows between 1 and {property.MaxGuests} guests.";
        if (property.HostId == userId)
            return "You cannot book your own property.";
        if (await HasOverlap(property.Id, model.CheckInDate, model.CheckOutDate))
            return "Those dates are no longer available. Please choose different dates.";

        return null;
    }

    private Task<bool> HasOverlap(int propertyId, DateTime checkIn, DateTime checkOut) =>
        _db.Bookings.AnyAsync(b => b.PropertyId == propertyId
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
            && b.CheckInDate < checkOut.Date
            && b.CheckOutDate > checkIn.Date);
}
