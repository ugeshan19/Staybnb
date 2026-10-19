using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Services;

namespace Staybnb.Controllers;

// ID documents sit outside wwwroot; only the guest who uploaded one or the host of that stay can fetch it.
[Authorize]
public class DocumentsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _files;

    public DocumentsController(ApplicationDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    public async Task<IActionResult> Download(int id)
    {
        var userId = CurrentUserId;
        var doc = await _db.GuestDocuments.AsNoTracking()
            .Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking).ThenInclude(b => b!.Property)
            .FirstOrDefaultAsync(d => d.Id == id);

        var booking = doc?.GuestCheckIn?.Booking;
        if (doc == null || booking == null) return NotFound();
        if (booking.GuestId != userId && booking.Property!.HostId != userId) return Forbid();

        var path = _files.GetDocumentPath(doc.FileName);
        if (path == null) return NotFound();

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
        return PhysicalFile(path, contentType);
    }
}
