using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Product.Data.Models.Users;

namespace Product.Api.Services;

public class AppClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
{
    public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        // Roles may be stored on `user.Role` (not-mapped) or as comma-separated `RoleRaw` in DB.
        IEnumerable<string> roles = Array.Empty<string>();
        if (user.Role is not null && user.Role.Any(r => !string.IsNullOrWhiteSpace(r)))
        {
            roles = user.Role.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(user.RoleRaw))
        {
            roles = user.RoleRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s));
        }

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(principal);
    }
}
