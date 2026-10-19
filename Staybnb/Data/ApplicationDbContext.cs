using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> HostProperties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasMany(u => u.HostedProperties).WithOne(p => p.Host).HasForeignKey(p => p.HostId);
            e.HasMany(u => u.Bookings).WithOne(x => x.Guest).HasForeignKey(x => x.GuestId);
            e.HasMany(u => u.Reviews).WithOne(r => r.Reviewer).HasForeignKey(r => r.ReviewerId);
        });

        b.Entity<HostProperty>(e =>
        {
            e.HasMany(p => p.Images).WithOne(i => i.Property).HasForeignKey(i => i.PropertyId);
            e.HasMany(p => p.Bookings).WithOne(x => x.Property).HasForeignKey(x => x.PropertyId);
            e.HasMany(p => p.Reviews).WithOne(r => r.Property).HasForeignKey(r => r.PropertyId);
            e.HasMany(p => p.Amenities).WithMany(a => a.Properties);                       // many-to-many
            e.HasOne(p => p.CheckInProcess).WithOne(c => c.Property).HasForeignKey<CheckInProcess>(c => c.PropertyId); // one-to-one
            e.Property(p => p.PropertyType).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(p => p.City);
        });

        b.Entity<Booking>(e =>
        {
            e.HasOne(x => x.Payment).WithOne(p => p.Booking).HasForeignKey<Payment>(p => p.BookingId);          // one-to-one
            e.HasOne(x => x.GuestCheckIn).WithOne(c => c.Booking).HasForeignKey<GuestCheckIn>(c => c.BookingId); // one-to-one
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Payment>().Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        b.Entity<HostApplication>(e =>
        {
            e.HasOne(a => a.ApplicationUser).WithMany().HasForeignKey(a => a.ApplicationUserId);
            e.HasOne(a => a.ReviewedBy).WithMany().HasForeignKey(a => a.ReviewedById);
            e.HasOne(a => a.Property).WithMany().HasForeignKey(a => a.PropertyId);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId);
            e.HasOne(m => m.Receiver).WithMany().HasForeignKey(m => m.ReceiverId);
            e.HasIndex(m => new { m.ReceiverId, m.IsRead });
        });

        b.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        b.Entity<ActivityLog>(e =>
        {
            e.Property(a => a.ActivityType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.CreatedAt);
        });

        b.Entity<GuestCheckIn>(e =>
        {
            e.HasOne(c => c.CheckInProcess).WithMany().HasForeignKey(c => c.CheckInProcessId);
            e.HasMany(c => c.Documents).WithOne(d => d.GuestCheckIn).HasForeignKey(d => d.GuestCheckInId);   // one-to-many
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<GuestDocument>().Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        b.Entity<WishlistItem>(e =>
        {
            e.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId);
            e.HasOne(w => w.Property).WithMany().HasForeignKey(w => w.PropertyId);
            e.HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();
        });

        // SQL Server refuses multiple cascade paths (User -> Property -> Booking, and User -> Booking),
        // so our own relationships use Restrict. The app doesn't hard-delete rows anyway.
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(HostProperty).Namespace)
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
