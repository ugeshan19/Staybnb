using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Helpers;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.AdminOrSuperAdmin)]
public class AdminController : BaseController
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IActivityLogger logger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Dashboard()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            PendingApplications = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await _db.HostProperties.CountAsync(p => p.IsActive && p.IsApproved),
            // the platform only earns the service fee on paid bookings
            PlatformRevenue = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Booking!.ServiceFee) ?? 0m,
            TotalBookings = await _db.Bookings.CountAsync(),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = (await _userManager.GetUsersInRoleAsync(Roles.Host)).Count,
            RecentActivity = await _db.ActivityLogs.AsNoTracking().Include(a => a.User).OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync(),
            RecentApplications = await _db.HostApplications.AsNoTracking().Include(a => a.ApplicationUser).Include(a => a.Property)
                .OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
            RecentBookings = await _db.Bookings.AsNoTracking().Include(b => b.Guest).Include(b => b.Property)
                .OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    public async Task<IActionResult> Applications(string? status)
    {
        var query = _db.HostApplications.AsNoTracking()
            .Include(a => a.ApplicationUser).Include(a => a.Property).ThenInclude(p => p!.Images).AsQueryable();
        if (Enum.TryParse<ApplicationStatus>(status, out var parsed)) query = query.Where(a => a.Status == parsed);

        ViewBag.Status = status;
        return View(await query.OrderBy(a => a.Status == ApplicationStatus.Pending ? 0 : 1).ThenByDescending(a => a.AppliedAt).ToListAsync());
    }

    public async Task<IActionResult> ApplicationDetails(int id)
    {
        var app = await _db.HostApplications.AsNoTracking().AsSplitQuery()
            .Include(a => a.ApplicationUser).Include(a => a.ReviewedBy)
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Include(a => a.Property).ThenInclude(p => p!.Amenities)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(app.ApplicationUser!);
        ViewBag.ApplicantRoles = roles;
        return View(app);
    }

    // Approving makes the applicant a Host and drops the Guest role; nobody holds both.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var app = await _db.HostApplications.Include(a => a.Property).Include(a => a.ApplicationUser)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        if (app.Status != ApplicationStatus.Pending)
        {
            Error("This application has already been processed.");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        var adminId = CurrentUserId;
        var applicant = app.ApplicationUser!;
        var promoted = false;

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (!await _userManager.IsInRoleAsync(applicant, Roles.Host))
        {
            var add = await _userManager.AddToRoleAsync(applicant, Roles.Host);
            if (!add.Succeeded) return RoleFailure(add, id);

            if (await _userManager.IsInRoleAsync(applicant, Roles.Guest))
            {
                var remove = await _userManager.RemoveFromRoleAsync(applicant, Roles.Guest);
                if (!remove.Succeeded) return RoleFailure(remove, id);
            }
            promoted = true;
        }

        app.Status = ApplicationStatus.Approved;
        app.ReviewedAt = DateTime.Now;
        app.ReviewedById = adminId;
        app.Property!.IsApproved = true;
        app.Property.IsActive = true;

        if (!await _db.CheckInProcesses.AnyAsync(c => c.PropertyId == app.PropertyId))
        {
            _db.CheckInProcesses.Add(new CheckInProcess
            {
                PropertyId = app.PropertyId, Title = $"Check-in for {app.Property.Title}", StepsJson = CheckInConfig.Default().ToJson()
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        if (promoted)
            await _logger.LogAsync(adminId, "RoleChanged", ActivityType.RoleChange, $"{applicant.Email}: Guest -> Host");
        await _logger.LogAsync(adminId, "HostApplicationApproved", ActivityType.Application, $"Application #{id} '{app.Property.Title}'");
        await _notifications.NotifyAsync(applicant.Id, "Host Application Approved",
            $"Congratulations! Your host application for '{app.Property.Title}' has been approved." + (promoted ? " You are now a Host." : string.Empty),
            NotificationType.Success, "/Host/Dashboard");

        Success($"Application approved. {applicant.FullName} {(promoted ? "is now a Host (Guest role removed)." : "can now list the new property.")}");
        return RedirectToAction(nameof(Applications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? notes)
    {
        var app = await _db.HostApplications.Include(a => a.Property).FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        if (app.Status != ApplicationStatus.Pending)
        {
            Error("This application has already been processed.");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        app.Status = ApplicationStatus.Rejected;
        app.ReviewedAt = DateTime.Now;
        app.ReviewedById = CurrentUserId;
        app.AdminNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim().Clip(500);
        await _db.SaveChangesAsync();

        await _logger.LogAsync(CurrentUserId, "HostApplicationRejected", ActivityType.Application, $"Application #{id} '{app.Property!.Title}'");
        await _notifications.NotifyAsync(app.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{app.Property.Title}' was not approved." + (app.AdminNotes != null ? $" Reason: {app.AdminNotes}" : string.Empty),
            NotificationType.Warning, "/HostApplication/Status");

        Success("Application rejected.");
        return RedirectToAction(nameof(Applications));
    }

    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Users(string? search, string? role)
    {
        var usersQuery = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            usersQuery = usersQuery.Where(u => u.Email!.Contains(s) || u.FirstName.Contains(s) || u.LastName.Contains(s));
        }
        var users = await usersQuery.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

        var roleMap = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name }).ToListAsync();
        var items = users.Select(u => new UserListItem
        {
            User = u,
            Roles = roleMap.Where(m => m.UserId == u.Id).Select(m => m.Name!).OrderBy(n => n).ToList()
        }).ToList();

        if (!string.IsNullOrWhiteSpace(role)) items = items.Where(i => i.Roles.Contains(role)).ToList();

        ViewBag.Search = search;
        ViewBag.Role = role;
        return View(items);
    }

    // SuperAdmin only, and only Guests are eligible
    [Authorize(Roles = Roles.SuperAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Promote(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, Roles.Guest))
        {
            Error("Only users with the Guest role can be promoted to Admin.");
            return RedirectToAction(nameof(Users));
        }

        var add = await _userManager.AddToRoleAsync(user, Roles.Admin);
        if (add.Succeeded) await _userManager.RemoveFromRoleAsync(user, Roles.Guest);
        else { Error(string.Join(" ", add.Errors.Select(e => e.Description))); return RedirectToAction(nameof(Users)); }

        await _logger.LogAsync(CurrentUserId, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Guest -> Admin");
        await _notifications.NotifyAsync(user.Id, "You are now an Admin", "A SuperAdmin promoted you to Administrator.", NotificationType.Success, "/Admin/Dashboard");
        Success($"{user.FullName} is now an Admin.");
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = Roles.SuperAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Demote(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, Roles.Admin) || await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            Error("Only Admins can be demoted.");
            return RedirectToAction(nameof(Users));
        }

        await _userManager.AddToRoleAsync(user, Roles.Guest);
        await _userManager.RemoveFromRoleAsync(user, Roles.Admin);
        await _logger.LogAsync(CurrentUserId, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Admin -> Guest");
        await _notifications.NotifyAsync(user.Id, "Role Updated", "Your Admin access was removed. You are now a Guest.", NotificationType.Warning, "/Properties");
        Success($"{user.FullName} is now a Guest.");
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> ActivityLogs(string? type, string? search, int page = 1)
    {
        var query = _db.ActivityLogs.AsNoTracking().Include(a => a.User).AsQueryable();
        if (Enum.TryParse<ActivityType>(type, out var parsed)) query = query.Where(a => a.ActivityType == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Action.Contains(s) || (a.Details != null && a.Details.Contains(s)) || a.User!.Email!.Contains(s));
        }

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        ViewBag.Type = type;
        ViewBag.Search = search;
        ViewBag.Total = total;

        return View(await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync());
    }

    public async Task<IActionResult> Bookings() =>
        View(await _db.Bookings.AsNoTracking().Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
            .OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync());

    public async Task<IActionResult> Properties() =>
        View(await _db.HostProperties.AsNoTracking().Include(p => p.Host).Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateProperty(int id)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id);
        if (property == null) return NotFound();

        property.IsActive = false;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(CurrentUserId, "PropertyDeactivatedByAdmin", ActivityType.Property, property.Title);
        await _notifications.NotifyAsync(property.HostId, "Listing Deactivated", $"An admin deactivated '{property.Title}'.", NotificationType.Warning, "/Host/Properties");
        Success("Property deactivated.");
        return RedirectToAction(nameof(Properties));
    }

    private IActionResult RoleFailure(IdentityResult result, int id)
    {
        Error("Could not update roles: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        return RedirectToAction(nameof(ApplicationDetails), new { id });
    }
}
