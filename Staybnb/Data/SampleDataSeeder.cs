using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

// Demo data so the dashboards aren't empty. Can be run repeatedly.
public static class SampleDataSeeder
{
    private const string Password = "Password@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var um = services.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await EnsureUser(um, "admin@staybnb.com", "Lunga", "Aphane", Roles.Admin);
        var elsie = await EnsureUser(um, "elsie@staybnb.com", "Elsie", "Masuku", Roles.Host);
        var neroshen = await EnsureUser(um, "neroshen@staybnb.com", "Neroshen", "Govender", Roles.Host);
        var banele = await EnsureUser(um, "banele@staybnb.com", "Banele", "Mthembu", Roles.Guest);
        var sboniso = await EnsureUser(um, "sboniso@staybnb.com", "Sboniso", "Mango", Roles.Guest);
        var trevor = await EnsureUser(um, "trevor@staybnb.com", "Trevor", "Mlotshwa", Roles.Guest);

        if (await db.HostProperties.AnyAsync()) return;

        // Amenities are created by RoleSeeder on every startup; just load them (in creation order)
        var amenities = await db.Amenities.OrderBy(x => x.Id).ToListAsync();

        HostProperty Make(ApplicationUser host, string title, string city, PropertyType type, decimal price, decimal cleaning,
                          int guests, int bedrooms, int beds, int baths, int imgStart, string description, bool approved = true)
        {
            var p = new HostProperty
            {
                Title = title,
                City = city,
                Country = "South Africa",
                PropertyType = type,
                PricePerNight = price,
                CleaningFee = cleaning,
                MaxGuests = guests,
                Bedrooms = bedrooms,
                Beds = beds,
                Bathrooms = baths,
                Address = $"{10 + imgStart * 7} Main Road, {city}",
                Description = description,
                HostId = host.Id,
                IsApproved = approved,
                IsActive = approved,
                CreatedAt = DateTime.Now.AddDays(-30 + imgStart)
            };

            // Each property owns a distinct set of 3 images: /uploads/seed/property-{1..7}-{1..3}.jpg (21 files total).
            for (var i = 1; i <= 3; i++)
                p.Images.Add(new PropertyImage { ImageUrl = $"/uploads/seed/property-{imgStart + 1}-{i}.jpg" });

            foreach (var a in amenities.Skip(imgStart % 4).Take(6)) p.Amenities.Add(a);
            return p;
        }

        var props = new List<HostProperty>
        {
            Make(elsie, "Luxury Beachfront Apartment", "Durban", PropertyType.Apartment, 1320, 250, 4, 2, 2, 1, 0, "Wake up to ocean views in this bright, modern apartment a few steps from the Golden Mile. Open-plan living, a fully equipped kitchen and a sunny balcony."),
            Make(elsie, "Cozy Forest Cabin", "Drakensberg", PropertyType.Cabin, 3100, 400, 8, 4, 5, 4, 1, "Escape to our cozy forest cabin tucked among towering trees and birdsong. Rustic charm, soft lighting and everything you need for a relaxing stay."),
            Make(neroshen, "Exceptional Villa in Nelspruit", "Nelspruit", PropertyType.Villa, 8325, 900, 10, 5, 6, 5, 2, "A private villa with a pool, entertainment deck and landscaped garden - perfect for family gatherings and group getaways."),
            Make(neroshen, "Umhlanga Beachfront Home", "Umhlanga", PropertyType.House, 2100, 350, 6, 3, 4, 2, 3, "Beautiful beachfront home on the Umhlanga promenade with sweeping sea views and a braai area."),
            Make(elsie, "Table Mountain View Loft", "Cape Town", PropertyType.Apartment, 1750, 300, 3, 1, 2, 1, 4, "Stylish loft in the city bowl with floor-to-ceiling windows framing Table Mountain."),
            Make(neroshen, "Karoo Stone Cottage", "Prince Albert", PropertyType.Cottage, 950, 150, 4, 2, 3, 1, 5, "Historic stone cottage in the Karoo village of Prince Albert. Quiet, starry nights and a shaded stoep."),
            Make(trevor, "Sandton Sky Studio", "Johannesburg", PropertyType.Apartment, 890, 120, 2, 1, 1, 1, 6, "Compact studio in Sandton close to Gautrain and Sandton City.", approved: false)
        };
        db.HostProperties.AddRange(props);
        await db.SaveChangesAsync();

        // Applications (6 approved, 1 pending)
        foreach (var p in props)
        {
            db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = p.HostId,
                PropertyId = p.Id,
                Status = p.IsApproved ? ApplicationStatus.Approved : ApplicationStatus.Pending,
                AppliedAt = p.CreatedAt,
                ReviewedAt = p.IsApproved ? p.CreatedAt.AddDays(1) : null,
                ReviewedById = p.IsApproved ? admin.Id : null
            });
            if (p.IsApproved)
                db.CheckInProcesses.Add(new CheckInProcess { PropertyId = p.Id, Title = $"Check-in for {p.Title}", StepsJson = CheckInConfig.Default().ToJson() });
        }
        await db.SaveChangesAsync();

        // Bookings: Pending, Approved+Paid, Completed+Paid(+review)
        Booking NewBooking(HostProperty p, ApplicationUser guest, int startOffset, int nights, int people, BookingStatus status)
        {
            var q = Services.BookingPricing.Calculate(p.PricePerNight, p.CleaningFee, DateTime.Today.AddDays(startOffset), DateTime.Today.AddDays(startOffset + nights));
            return new Booking
            {
                PropertyId = p.Id,
                GuestId = guest.Id,
                Guests = people,
                CheckInDate = DateTime.Today.AddDays(startOffset),
                CheckOutDate = DateTime.Today.AddDays(startOffset + nights),
                NightlyRate = q.NightlyRate,
                CleaningFee = q.CleaningFee,
                ServiceFee = q.ServiceFee,
                TotalPrice = q.Total,
                Status = status,
                CreatedAt = DateTime.Now.AddDays(startOffset > 0 ? -2 : startOffset - 3),
                Payment = new Payment
                {
                    Amount = q.Total,
                    CreatedAt = DateTime.Now,
                    Status = status == BookingStatus.Pending ? PaymentStatus.Pending : PaymentStatus.Paid
                }
            };
        }

        var b1 = NewBooking(props[1], banele, 14, 2, 4, BookingStatus.Pending);
        var b2 = NewBooking(props[0], sboniso, 7, 3, 2, BookingStatus.Approved);
        var b3 = NewBooking(props[3], banele, -20, 2, 3, BookingStatus.Completed);
        var b4 = NewBooking(props[4], sboniso, -12, 4, 2, BookingStatus.Completed);
        db.Bookings.AddRange(b1, b2, b3, b4);
        await db.SaveChangesAsync();

        db.Reviews.AddRange(
            new Review { PropertyId = props[3].Id, ReviewerId = banele.Id, BookingId = b3.Id, Rating = 5, Comment = "Amazing views and a lovely host. Would stay again!" },
            new Review { PropertyId = props[4].Id, ReviewerId = sboniso.Id, BookingId = b4.Id, Rating = 4, Comment = "Great location, very clean loft." });

        db.Notifications.AddRange(
            new Notification { UserId = elsie.Id, Title = "New Booking Request", Message = "Banele Mthembu requested Cozy Forest Cabin.", Type = NotificationType.Booking, Link = "/Host/Bookings" },
            new Notification { UserId = admin.Id, Title = "New Host Application", Message = "Trevor Mlotshwa applied to host Sandton Sky Studio.", Type = NotificationType.Application, Link = "/Admin/Applications" });

        db.Messages.Add(new Message { SenderId = banele.Id, ReceiverId = elsie.Id, Content = "Hi Elsie, is early check-in possible for the cabin?", Timestamp = DateTime.Now.AddHours(-3) });

        db.ActivityLogs.AddRange(
            new ActivityLog { UserId = trevor.Id, Action = "HostApplicationSubmitted", ActivityType = ActivityType.Application, Details = "Sandton Sky Studio" },
            new ActivityLog { UserId = admin.Id, Action = "HostApplicationApproved", ActivityType = ActivityType.RoleChange, Details = "Elsie Masuku promoted Guest -> Host" },
            new ActivityLog { UserId = banele.Id, Action = "BookingRequested", ActivityType = ActivityType.Booking, Details = "Cozy Forest Cabin" });
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> um, string email, string first, string last, string role)
    {
        var user = await um.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = first, LastName = last };
            var result = await um.CreateAsync(user, Password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Seed user failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (!await um.IsInRoleAsync(user, role)) await um.AddToRoleAsync(user, role);
        return user;
    }
}