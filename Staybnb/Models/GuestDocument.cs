using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class GuestDocument
{
    public int Id { get; set; }

    public int GuestCheckInId { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }

    [Required, StringLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    // GUID file name under App_Data/documents (not publicly served)
    [Required, StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(200)]
    public string OriginalName { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    [StringLength(300)]
    public string? RejectionReason { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
