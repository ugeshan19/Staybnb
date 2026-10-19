using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Staybnb.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();

    [NotMapped]
    public string Initials =>
        $"{(FirstName.Length > 0 ? FirstName[0] : '?')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpper();

    public ICollection<HostProperty> HostedProperties { get; set; } = new List<HostProperty>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
