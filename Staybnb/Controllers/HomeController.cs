using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

public class HomeController : Controller
{
    // Everyone lands on a different page depending on their role.
    public IActionResult Index()
    {
        if (User.IsInRole(Roles.Host))
            return RedirectToAction("Dashboard", "Host");
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin))
            return RedirectToAction("Dashboard", "Admin");

        return RedirectToAction("Index", "Properties");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
