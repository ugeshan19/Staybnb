using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guest check-in wizard: house rules, then ID upload, then review and submit. The host verifies afterwards.
[Authorize(Roles = Roles.Guest)]
public class CheckInController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public CheckInController(ApplicationDbContext db, IFileStorage files, IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _files = files;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(int bookingId, int? step)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Approved && booking.Status != BookingStatus.CheckedIn)
        {
            Error("Check-in is only available for approved bookings.");
            return RedirectToAction("Details", "Bookings", new { id = bookingId });
        }

        var checkIn = await EnsureCheckIn(booking);
        var locked = checkIn.Status == CheckInStatus.Submitted || checkIn.Status == CheckInStatus.Verified;
        var s = locked ? 3 : Math.Clamp(step ?? checkIn.CurrentStep, 1, checkIn.CurrentStep);

        return View(new CheckInStepViewModel
        {
            Booking = booking, CheckIn = checkIn, Step = s,
            Config = CheckInConfig.Parse(checkIn.CheckInProcess?.StepsJson)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcknowledgeRules(int bookingId, bool accepted)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        var checkIn = await EnsureCheckIn(booking);

        if (!accepted)
        {
            Error("Please accept the house rules to continue.");
            return RedirectToAction(nameof(Index), new { bookingId, step = 1 });
        }
        checkIn.RulesAcknowledged = true;
        if (checkIn.CurrentStep < 2) checkIn.CurrentStep = 2;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { bookingId, step = 2 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(int bookingId, string documentType, IFormFile? document)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        var checkIn = await EnsureCheckIn(booking);

        if (checkIn.Status == CheckInStatus.Submitted || checkIn.Status == CheckInStatus.Verified)
        {
            Error("This check-in has already been submitted.");
            return RedirectToAction(nameof(Index), new { bookingId });
        }
        if (string.IsNullOrWhiteSpace(documentType))
        {
            Error("Please choose the document type.");
            return RedirectToAction(nameof(Index), new { bookingId, step = 2 });
        }
        var err = document == null ? "Please choose a file to upload." : _files.Validate(document, isDocument: true);
        if (err != null)
        {
            Error(err);
            return RedirectToAction(nameof(Index), new { bookingId, step = 2 });
        }

        var stored = await _files.SaveDocumentAsync(document!);
        _db.GuestDocuments.Add(new GuestDocument
        {
            GuestCheckInId = checkIn.Id, DocumentType = documentType.Trim(),
            FileName = stored, OriginalName = Path.GetFileName(document!.FileName), Status = DocumentStatus.Pending
        });
        if (checkIn.Status == CheckInStatus.Rejected) checkIn.Status = CheckInStatus.InProgress;
        if (checkIn.CurrentStep < 3) checkIn.CurrentStep = 3;
        await _db.SaveChangesAsync();

        Success("Document uploaded.");
        return RedirectToAction(nameof(Index), new { bookingId, step = 3 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDocument(int bookingId, int documentId)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        var checkIn = await EnsureCheckIn(booking);
        var doc = await _db.GuestDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.GuestCheckInId == checkIn.Id);
        if (doc != null && checkIn.Status != CheckInStatus.Submitted && checkIn.Status != CheckInStatus.Verified)
        {
            _files.DeleteDocument(doc.FileName);
            _db.GuestDocuments.Remove(doc);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { bookingId, step = 3 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int bookingId)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        var checkIn = await EnsureCheckIn(booking);

        var hasDocs = await _db.GuestDocuments.AnyAsync(d => d.GuestCheckInId == checkIn.Id && d.Status != DocumentStatus.Rejected);
        if (!checkIn.RulesAcknowledged || !hasDocs)
        {
            Error("Accept the house rules and upload at least one document before submitting.");
            return RedirectToAction(nameof(Index), new { bookingId });
        }

        checkIn.Status = CheckInStatus.Submitted;
        checkIn.SubmittedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var uid = CurrentUserId;
        await _logger.LogAsync(uid, "CheckInSubmitted", ActivityType.CheckIn, $"Booking #{bookingId}");
        await _notifications.NotifyAsync(booking.Property!.HostId, "Check-in Documents Submitted",
            $"A guest submitted their check-in documents for '{booking.Property.Title}'. Please verify.", NotificationType.CheckIn, $"/Host/CheckInReview/{checkIn.Id}");

        Success("Check-in submitted. The host will verify your documents.");
        return RedirectToAction("Details", "Bookings", new { id = bookingId });
    }

    private async Task<Booking?> LoadBooking(int bookingId)
    {
        var uid = CurrentUserId;
        return await _db.Bookings.Include(b => b.Property).FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == uid);
    }

    // Gets or creates the guest's check-in. If the host never set up a process for this property, a default one is created too.
    private async Task<GuestCheckIn> EnsureCheckIn(Booking booking)
    {
        var checkIn = await _db.GuestCheckIns.Include(c => c.CheckInProcess).Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.BookingId == booking.Id);
        if (checkIn != null) return checkIn;

        var process = await _db.CheckInProcesses.FirstOrDefaultAsync(p => p.PropertyId == booking.PropertyId);
        if (process == null)
        {
            process = new CheckInProcess
            {
                PropertyId = booking.PropertyId, Title = $"Check-in for {booking.Property!.Title}",
                StepsJson = CheckInConfig.Default().ToJson()
            };
            _db.CheckInProcesses.Add(process);
            await _db.SaveChangesAsync();
        }

        checkIn = new GuestCheckIn { BookingId = booking.Id, CheckInProcessId = process.Id, Status = CheckInStatus.InProgress };
        _db.GuestCheckIns.Add(checkIn);
        await _db.SaveChangesAsync();
        checkIn.CheckInProcess = process;
        return checkIn;
    }
}
