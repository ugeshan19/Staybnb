using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class AccountController : BaseController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IActivityLogger _logger;
    private readonly INotificationService _notifications;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IActivityLogger logger, INotificationService notifications)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _notifications = notifications;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    // everyone starts out as a Guest
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, Roles.Guest);
        await _logger.LogAsync(user.Id, "UserRegistered", ActivityType.Registration, $"{user.Email} registered as Guest");
        await _notifications.NotifyAsync(user.Id, "Welcome to Staybnb", "Your account is ready. Explore stays or become a host.", NotificationType.Success, "/Properties");

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _logger.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Account locked after too many attempts. Try again in a few minutes."
            : "Invalid email or password.");

        await _logger.LogAsync(user.Id, "LoginFailed", ActivityType.Login, "Failed login attempt");
        return View(model);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = CurrentUserId;
        await _signInManager.SignOutAsync();
        await _logger.LogAsync(userId, "UserLoggedOut", ActivityType.Logout);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
