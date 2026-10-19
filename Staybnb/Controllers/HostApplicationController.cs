using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guests use this to apply for host status. Existing hosts reuse it to list another property.
[Authorize(Roles = "Guest,Host")]
public class HostApplicationController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _files;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public HostApplicationController(ApplicationDbContext db, IFileStorage files, IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _files = files;
        _logger = logger;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var uid = CurrentUserId;
        if (await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == uid && a.Status == ApplicationStatus.Pending))
        {
            Error("You already have an application under review. You'll be notified once an admin decides.");
            return RedirectToAction(nameof(Status));
        }
        return View(new PropertyFormViewModel { AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel model)
    {
        var uid = CurrentUserId;
        model.AllAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();

        if (await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == uid && a.Status == ApplicationStatus.Pending))
        {
            Error("You already have an application under review.");
            return RedirectToAction(nameof(Status));
        }

        // at least one photo is mandatory
        var files = (model.Images ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
        if (files.Count == 0)
            ModelState.AddModelError(nameof(model.Images), "Please upload at least one property image.");
        foreach (var f in files)
        {
            var err = _files.Validate(f, isDocument: false);
            if (err != null) ModelState.AddModelError(nameof(model.Images), err);
        }
        if (!ModelState.IsValid) return View(model);

        var saved = new List<string>();
        try
        {
            foreach (var f in files) saved.Add(await _files.SaveImageAsync(f));

            var property = new HostProperty
            {
                Title = model.Title.Trim(), Description = model.Description.Trim(),
                PricePerNight = model.PricePerNight, CleaningFee = model.CleaningFee,
                Address = model.Address.Trim(), City = model.City.Trim(), Country = model.Country.Trim(),
                PropertyType = model.PropertyType, MaxGuests = model.MaxGuests, Bedrooms = model.Bedrooms,
                Beds = model.Beds, Bathrooms = model.Bathrooms,
                HostId = uid, IsActive = false, IsApproved = false
            };
            foreach (var url in saved) property.Images.Add(new PropertyImage { ImageUrl = url });
            var amenities = await _db.Amenities.Where(a => model.SelectedAmenityIds.Contains(a.Id)).ToListAsync();
            foreach (var a in amenities) property.Amenities.Add(a);

            _db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = uid, Property = property, Status = ApplicationStatus.Pending, AppliedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            foreach (var url in saved) _files.DeleteImage(url);
            throw;
        }

        var isHost = User.IsInRole(Roles.Host);
        await _logger.LogAsync(uid, isHost ? "AdditionalPropertyApplicationSubmitted" : "HostApplicationSubmitted", ActivityType.Application, model.Title);
        await _notifications.NotifyAsync(uid, isHost ? "Additional Property Application Submitted" : "Host Application Submitted",
            $"Your application for '{model.Title}' has been received and is under review.", NotificationType.Application, "/HostApplication/Status");
        await _notifications.NotifyAdminsAsync("New Host Application", $"A new application for '{model.Title}' needs review.", NotificationType.Application, "/Admin/Applications");

        Success("Application submitted! An admin will review it shortly.");
        return RedirectToAction(nameof(Status));
    }

    public async Task<IActionResult> Status()
    {
        var uid = CurrentUserId;
        var apps = await _db.HostApplications.AsNoTracking()
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Where(a => a.ApplicationUserId == uid)
            .OrderByDescending(a => a.AppliedAt).ToListAsync();
        return View(apps);
    }
}
