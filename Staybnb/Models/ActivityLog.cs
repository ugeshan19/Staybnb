using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class ActivityLog
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    public ActivityType ActivityType { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
