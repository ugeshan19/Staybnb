using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class GuestCheckIn
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int CheckInProcessId { get; set; }
    public CheckInProcess? CheckInProcess { get; set; }

    public CheckInStatus Status { get; set; } = CheckInStatus.InProgress;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // furthest step reached: 1 rules, 2 documents, 3 confirm
    public int CurrentStep { get; set; } = 1;
    public bool RulesAcknowledged { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
}
