using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.Info;

    public bool IsRead { get; set; }

    // optional local URL behind the "Take Action" button
    [StringLength(200)]
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
