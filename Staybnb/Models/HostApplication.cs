using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class HostApplication
{
    public int Id { get; set; }

    [Required]
    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser? ApplicationUser { get; set; }

    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime AppliedAt { get; set; } = DateTime.Now;

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }

    [StringLength(500)]
    public string? AdminNotes { get; set; }
}
