using Staybnb.Models;

namespace Staybnb.ViewModels;

public class HostDashboardViewModel
{
    public string HostName { get; set; } = string.Empty;
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int BookingsThisMonth { get; set; }
    public int PendingRequests { get; set; }
    public int PendingCheckIns { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal EarningsThisMonth { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public double AcceptanceRate { get; set; }
    public decimal AverageBookingValue { get; set; }
    public List<Booking> RecentBookings { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveProperties { get; set; }
    public decimal PlatformRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }
    public List<ActivityLog> RecentActivity { get; set; } = new();
    public List<HostApplication> RecentApplications { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
}

public class UserListItem
{
    public ApplicationUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
}

public class InboxItem
{
    public ApplicationUser OtherUser { get; set; } = null!;
    public Message LastMessage { get; set; } = null!;
    public int Unread { get; set; }
}

public class ThreadViewModel
{
    public ApplicationUser OtherUser { get; set; } = null!;
    public List<Message> Messages { get; set; } = new();
}

public class TopBarViewModel
{
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
    public List<Notification> Recent { get; set; } = new();
}
