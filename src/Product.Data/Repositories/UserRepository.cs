using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Data.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    private static List<string> DeserializeRoles(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string> { "USER" };
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string SerializeRoles(List<string>? roles)
    {
        if (roles is null || !roles.Any())
            return "USER";
        return string.Join(
            ',',
            roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim())
        );
    }

    public async Task<ApplicationUser?> GetUserWithPersonalDataAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;
        user.Role = DeserializeRoles(user.RoleRaw);
        return user;
    }

    public async Task<ApplicationUser?> GetUserWithPersonalDataByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        var user = await db
            .Users.Include(u => u.PersonalData)
            .ThenInclude(pd => pd!.Address)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
            return null;
        user.Role = DeserializeRoles(user.RoleRaw);
        return user;
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
            return null;
        user.Role = DeserializeRoles(user.RoleRaw);
        return user;
    }

    public async Task<bool> IsEmailTakenAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(
            u => u.Id != userId && u.NormalizedEmail == normalizedEmail,
            ct
        );
    }

    public async Task<bool> IsUsernameTakenAsync(
        Guid userId,
        string normalizedUsername,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(
            u => u.Id != userId && u.NormalizedUserName == normalizedUsername,
            ct
        );
    }

    public async Task<bool> IsCpfTakenAsync(string cpf, Guid userId, CancellationToken ct = default)
    {
        return await db.UserPersonalData.AnyAsync(x => x.UserId != userId && x.Cpf == cpf, ct);
    }

    public async Task<bool> UserNameExistsAsync(
        string normalizedUsername,
        CancellationToken ct = default
    )
    {
        return await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername, ct);
    }

    public async Task EnsurePersonalDataAsync(Guid userId, CancellationToken ct = default)
    {
        if (await db.UserPersonalData.AnyAsync(x => x.UserId == userId, ct))
        {
            return;
        }

        db.UserPersonalData.Add(new UserPersonalData { UserId = userId });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        user.RoleRaw = SerializeRoles(user.Role);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string[]> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Array.Empty<string>();
        var roles = DeserializeRoles(user.RoleRaw);
        user.Role = roles;
        return roles.ToArray();
    }

    public async Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await db
            .RefreshTokens.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyCollection<ApplicationUser> Users, int Total)>
        SearchUsersAsync(
            string? query,
            string? by,
            bool startsWith,
            int page,
            int pageSize,
            CancellationToken ct = default
        )
    {
        var q = db.Users.AsQueryable();

        // include personal data for richer response
        q = q.Include(u => u.PersonalData).ThenInclude(pd => pd!.Address);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = startsWith ? query + "%" : "%" + query + "%";
            var normalized = query.Trim();

            if (!string.IsNullOrWhiteSpace(by))
            {
                var key = by.Trim().ToLowerInvariant();
                if (key == "name")
                {
                    q = q.Where(u => EF.Functions.Like(u.Name!, pattern));
                }
                else if (key == "username" || key == "user" || key == "username")
                {
                    q = q.Where(u => EF.Functions.Like(u.UserName!, pattern));
                }
                else if (key == "email")
                {
                    q = q.Where(u => EF.Functions.Like(u.Email!, pattern));
                }
                else
                {
                    q = q.Where(u => EF.Functions.Like(u.Name!, pattern) || EF.Functions.Like(u.UserName!, pattern));
                }
            }
            else
            {
                q = q.Where(u => EF.Functions.Like(u.Name!, pattern) || EF.Functions.Like(u.UserName!, pattern));
            }
        }

        var total = await q.CountAsync(ct);

        var users = await q
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // ensure Role deserialization
        foreach (var u in users)
        {
            u.Role = DeserializeRoles(u.RoleRaw);
        }

        return (users, total);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        return await db.RefreshTokens.FirstOrDefaultAsync(
            x => x.Id == sessionId && x.UserId == userId,
            ct
        );
    }

    public async Task UpdateUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        user.RoleRaw = SerializeRoles(user.Role);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
