using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

// Runs on every startup: makes sure the four roles, the SuperAdmin account and the
// default amenities exist (amenities are needed by the Become-a-Host form).
public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var email = config["SuperAdmin:Email"] ?? "superadmin@staybnb.com";
        var password = config["SuperAdmin:Password"] ?? "SuperAdmin@123";

        var superAdmin = await userManager.FindByEmailAsync(email);
        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin"
            };
            var result = await userManager.CreateAsync(superAdmin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not create SuperAdmin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(superAdmin, Roles.SuperAdmin))
            await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);

        await SeedAmenitiesAsync(db);
    }

    private static readonly (string Name, string Icon)[] DefaultAmenities =
    {
        ("Wifi", "fa-solid fa-wifi"), ("Kitchen", "fa-solid fa-utensils"), ("Air Conditioning", "fa-solid fa-snowflake"),
        ("Heating", "fa-solid fa-fire"), ("Free Parking", "fa-solid fa-square-parking"), ("TV", "fa-solid fa-tv"),
        ("Pool", "fa-solid fa-person-swimming"), ("Washer", "fa-solid fa-soap"), ("Coffee Maker", "fa-solid fa-mug-hot"),
        ("Garden", "fa-solid fa-tree")
    };

    // Adds any default amenity that is not in the database yet (safe to run on every startup).
    public static async Task SeedAmenitiesAsync(ApplicationDbContext db)
    {
        var existing = await db.Amenities.Select(a => a.Name).ToListAsync();
        var missing = DefaultAmenities.Where(a => !existing.Contains(a.Name));
        foreach (var (name, icon) in missing)
            db.Amenities.Add(new Amenity { Name = name, IconClass = icon });
        await db.SaveChangesAsync();
    }
}