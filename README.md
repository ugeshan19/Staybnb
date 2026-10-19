# Staybnb

A full-stack Airbnb-style property rental platform built with **ASP.NET Core 8 MVC**, **Entity Framework Core 8**, **SQL Server** and **ASP.NET Core Identity**.

Guests browse and book properties, hosts list and manage them, and admins approve hosts and audit activity, all behind role-based access control.

## Features

- **Role-based authentication** – Guest, Host, Admin and SuperAdmin roles (ASP.NET Core Identity). New sign-ups are Guests; role changes take effect immediately without re-login.
- **Host application workflow** – users apply to become hosts with property details and mandatory photos; admins approve or reject.
- **Host dashboard** – manage properties (active/inactive toggle), approve or reject booking requests, configure check-in rules, view earnings and reviews.
- **Booking engine** – date and guest selection with live pricing: `(price per night × nights) + cleaning fee + 12% service fee`. Status flow: Pending → Approved → CheckedIn → Completed.
- **Digital check-in** – guests accept house rules and upload ID; hosts verify the document and confirm check-in. Documents are stored outside `wwwroot` and streamed only to the guest and the property's host.
- **Messaging and notifications** – in-app inbox with unread counts and a notification bell.
- **Admin tools** – host requests, user management, SuperAdmin-only promotion to Admin, and a searchable activity log.
- **Wishlist, property filtering** (city, type) and simulated payments.

## Tech stack

C# · ASP.NET Core 8 MVC · EF Core 8 (code-first migrations) · SQL Server / LocalDB · ASP.NET Core Identity · Razor views · Bootstrap · jQuery

## Architecture

```
Staybnb/
├── Controllers/       Account, Admin, Host, Bookings, CheckIn, Messages, ...
├── Models/            Booking, HostProperty, GuestCheckIn, Payment, Review, ...
├── ViewModels/
├── Services/          BookingPricing, ActivityLogger, NotificationService, FileStorageService
├── Data/              ApplicationDbContext, RoleSeeder
├── ViewComponents/    TopBar (notifications / messages)
├── Migrations/
└── Views/
```

## Run locally

Requires Visual Studio 2022, the .NET 8 SDK and SQL Server LocalDB (included with Visual Studio).

1. Open `Staybnb.sln`.
2. Press **F5**. The included `InitialCreate` migration is applied on startup, and roles, the SuperAdmin and demo data are seeded.

To use full SQL Server, edit `ConnectionStrings:DefaultConnection` in `Staybnb/appsettings.json`. Set `"Seed:SampleData": false` to start without demo data.

### Demo accounts (local demo data only)

| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@staybnb.com | SuperAdmin@123 |
| Admin | admin@staybnb.com | Password@123 |
| Host | elsie@staybnb.com | Password@123 |
| Guest | banele@staybnb.com | Password@123 |

> These are seed credentials for local demos only. Change them (or use user-secrets) before deploying anywhere.

## Suggested walkthrough

Register → apply to become a host → approve as admin → list a property → book as a guest → approve as host → guest check-in → host verifies and confirms → review the activity log.

## Author

Ugeshan Naicker · Built as a Programming 742 assignment at Richfield.
