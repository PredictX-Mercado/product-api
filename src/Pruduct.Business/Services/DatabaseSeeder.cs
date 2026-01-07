using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pruduct.Business.Abstractions;
using Pruduct.Common.Enums;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models;

namespace Pruduct.Business.Services;

public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseSeeder(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task SeedAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        var roles = new[]
        {
            RoleName.USER,
            RoleName.ADMIN_L1,
            RoleName.ADMIN_L2,
            RoleName.ADMIN_L3,
        };
        foreach (var roleName in roles)
        {
            if (!await _db.Roles.AnyAsync(r => r.Name == roleName, ct))
            {
                _db.Roles.Add(new Role { Name = roleName });
            }
        }

        await _db.SaveChangesAsync(ct);

        var seedSection = configuration.GetSection("Seed:Admin");
        var adminEmail = seedSection.GetValue<string>("Email");
        var adminUsername = seedSection.GetValue<string>("Username") ?? "admin";
        var adminName = seedSection.GetValue<string>("Name") ?? "Admin";
        var adminPassword = seedSection.GetValue<string>("Password") ?? "ChangeMe123!";
        var adminCpf = seedSection.GetValue<string>("Cpf") ?? "00000000000";
        var adminAddress = seedSection.GetValue<string>("Address") ?? "N/A";

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(adminEmail);
        var normalizedName = NormalizeName(adminName);
        var username = string.IsNullOrWhiteSpace(adminUsername)
            ? ExtractUsernameFromEmail(normalizedEmail)
            : NormalizeUsername(adminUsername);

        var admin = await _db
            .Users.Include(u => u.PersonalData)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (admin is null)
        {
            admin = new User
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                Username = username,
                NormalizedUsername = username,
                Name = adminName,
                NormalizedName = normalizedName,
                PasswordHash = _hasher.Hash(adminPassword),
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = adminCpf,
                    Address = adminAddress,
                    PhoneNumber = null,
                },
            };

            _db.Users.Add(admin);
            await _db.SaveChangesAsync(ct);
        }

        var adminRole = await _db.Roles.FirstAsync(r => r.Name == RoleName.ADMIN_L3, ct);
        if (
            !await _db.UserRoles.AnyAsync(
                ur => ur.UserId == admin.Id && ur.RoleName == adminRole.Name,
                ct
            )
        )
        {
            _db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleName = adminRole.Name });
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string ExtractUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return NormalizeUsername(email);
        }

        var localPart = email[..atIndex];
        var value = string.IsNullOrWhiteSpace(localPart) ? email : localPart;
        return NormalizeUsername(value);
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string NormalizeUsername(string username) =>
        RemoveDiacritics(username).Trim().ToLowerInvariant();

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
