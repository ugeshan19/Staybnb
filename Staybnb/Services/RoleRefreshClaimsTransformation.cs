using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Staybnb.Models;

namespace Staybnb.Services;

// Roles are stored in the login cookie, so a newly approved host would need to log out and in again.
// This reloads the roles from the database on every request so the change shows up straight away.
public class RoleRefreshClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleRefreshClaimsTransformation(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null || !identity.IsAuthenticated) return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return principal;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return principal;

        foreach (var stale in identity.FindAll(identity.RoleClaimType).ToList())
            identity.RemoveClaim(stale);

        foreach (var role in await _userManager.GetRolesAsync(user))
            identity.AddClaim(new Claim(identity.RoleClaimType, role));

        return principal;
    }
}
