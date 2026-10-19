using System.Globalization;

namespace Staybnb.Helpers;

public static class UiHelper
{
    public static string Money(decimal value) => "R " + value.ToString("N2", CultureInfo.InvariantCulture);

    public static string Badge(string status) => status switch
    {
        "Approved" or "Verified" or "Paid" or "Completed" or "Active" or "Success" => "badge-soft-success",
        "Pending" or "Submitted" or "InProgress" => "badge-soft-warning",
        "Rejected" or "Cancelled" or "Refunded" or "Inactive" => "badge-soft-danger",
        "CheckedIn" => "badge-soft-info",
        _ => "badge-soft-secondary"
    };

    public static string Img(string? url) => string.IsNullOrWhiteSpace(url) ? "/images/placeholder.svg" : url;

    public static string Ago(DateTime when)
    {
        var elapsed = DateTime.Now - when;

        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";

        return when.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }
}
