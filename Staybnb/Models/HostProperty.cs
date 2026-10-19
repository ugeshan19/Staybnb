using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerNight { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CleaningFee { get; set; }

    [Required, StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Country { get; set; } = "South Africa";

    public PropertyType PropertyType { get; set; }

    public int MaxGuests { get; set; } = 2;
    public int Bedrooms { get; set; } = 1;
    public int Beds { get; set; } = 1;
    public int Bathrooms { get; set; } = 1;

    // the host's on/off switch; only matters once IsApproved is set
    public bool IsActive { get; set; }

    // flipped by an admin when the application is approved
    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Required]
    public string HostId { get; set; } = string.Empty;
    public ApplicationUser? Host { get; set; }

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public CheckInProcess? CheckInProcess { get; set; }

    [NotMapped]
    public double AverageRating => Reviews != null && Reviews.Count > 0 ? Reviews.Average(r => r.Rating) : 0;

    [NotMapped]
    public string? CoverImage => Images?.OrderBy(i => i.Id).FirstOrDefault()?.ImageUrl;
}
