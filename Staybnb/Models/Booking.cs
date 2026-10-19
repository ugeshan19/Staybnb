using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }

    [Required]
    public string GuestId { get; set; } = string.Empty;
    public ApplicationUser? Guest { get; set; }

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Guests { get; set; } = 1;

    // rate at the time of booking, so later price changes don't touch old bookings
    [Column(TypeName = "decimal(18,2)")]
    public decimal NightlyRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CleaningFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceFee { get; set; }

    // nightly rate * nights + cleaning fee + service fee
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Payment? Payment { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }

    [NotMapped]
    public int Nights => Math.Max(0, (CheckOutDate.Date - CheckInDate.Date).Days);
}
