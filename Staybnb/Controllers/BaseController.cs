using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Staybnb.Controllers;

public abstract class BaseController : Controller
{
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;
}
