using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class PropertyIndexViewModel
{
    public List<HostProperty> Properties { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public HashSet<int> WishlistIds { get; set; } = new();
    public string? City { get; set; }
    public PropertyType? Type { get; set; }
}

public class PropertyDetailsViewModel
{
    public HostProperty Property { get; set; } = null!;
    public bool IsWishlisted { get; set; }
    public bool IsGuest { get; set; }
    public BookingRequestViewModel Booking { get; set; } = new();
}

public class BookingRequestViewModel
{
    public int PropertyId { get; set; }

    [DataType(DataType.Date), Display(Name = "Check-in")]
    public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(1);

    [DataType(DataType.Date), Display(Name = "Check-out")]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(2);

    [Range(1, 50)]
    public int Guests { get; set; } = 1;
}

// shared by the "become a host" form and the edit-listing form
public class PropertyFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000000), Display(Name = "Price per night (R)")]
    public decimal PricePerNight { get; set; }

    [Range(0, 1000000), Display(Name = "Cleaning fee (R)")]
    public decimal CleaningFee { get; set; }

    [Required, StringLength(250), Display(Name = "Street address")]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Country { get; set; } = "South Africa";

    [Display(Name = "Property type")]
    public PropertyType PropertyType { get; set; }

    [Range(1, 50), Display(Name = "Max guests")]
    public int MaxGuests { get; set; } = 2;

    [Range(0, 50)]
    public int Bedrooms { get; set; } = 1;

    [Range(1, 50)]
    public int Beds { get; set; } = 1;

    [Range(1, 50)]
    public int Bathrooms { get; set; } = 1;

    public List<int> SelectedAmenityIds { get; set; } = new();

    [Display(Name = "Photos")]
    public List<IFormFile>? Images { get; set; }

    // only used to render the form
    public List<Amenity> AllAmenities { get; set; } = new();
    public List<PropertyImage> ExistingImages { get; set; } = new();
}

public class CheckInStepViewModel
{
    public Booking Booking { get; set; } = null!;
    public GuestCheckIn CheckIn { get; set; } = null!;
    public CheckInConfig Config { get; set; } = new();
    public int Step { get; set; } = 1;
}

public class CheckInConfigViewModel
{
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Instructions { get; set; }

    [Display(Name = "House rules (one per line)")]
    public string? RulesText { get; set; }

    [Display(Name = "Required documents (one per line)")]
    public string? DocumentsText { get; set; }
}
