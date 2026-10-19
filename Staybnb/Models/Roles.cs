namespace Staybnb.Models;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Host = "Host";
    public const string Guest = "Guest";
    public const string AdminOrSuperAdmin = "Admin,SuperAdmin";

    public static readonly string[] All = { SuperAdmin, Admin, Host, Guest };
}
