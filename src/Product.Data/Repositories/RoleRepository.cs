using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;

namespace Product.Data.Repositories;

public class RoleRepository(AppDbContext db) : IRoleRepository
{
    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(
            u =>
                u.RoleRaw != null
                && (
                    u.RoleRaw == roleName
                    || u.RoleRaw.StartsWith(roleName + ",")
                    || u.RoleRaw.Contains("," + roleName + ",")
                    || u.RoleRaw.EndsWith("," + roleName)
                ),
            ct
        );
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
