using Product.Business.Interfaces.Users;
using Product.Common.Enums;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;

namespace Product.Business.Services.Users;

public class RolePromotionService(IUserRepository userRepository) : IRolePromotionService
{
    private readonly IUserRepository _userRepository = userRepository;

    private static bool IsProtectedRole(string role)
    {
        return string.Equals(role?.Trim(), "USER", StringComparison.OrdinalIgnoreCase);
    }

    public async Task PromoteToRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken ct = default
    )
    {
        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        if (user is null)
            throw new InvalidOperationException("user_not_found");

        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("invalid_role", nameof(roleName));

        var roles = user.Role ?? new List<string> { "USER" };
        var needsSave = false;

        // If role not present, add and mark to save
        if (!roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add(roleName);
            needsSave = true;
        }

        // If underlying raw column is not set, ensure we persist current roles
        if (string.IsNullOrWhiteSpace(user.RoleRaw))
            needsSave = true;

        if (needsSave)
        {
            user.Role = roles;
            await _userRepository.UpdateUserAsync(user, ct);
        }
    }

    public async Task PromoteToAdminLevelAsync(
        Guid userId,
        int level,
        CancellationToken ct = default
    )
    {
        if (level < 1 || level > 3)
            throw new ArgumentOutOfRangeException(nameof(level));

        var roleName = level switch
        {
            1 => RoleName.ADMIN_L1.ToString(),
            2 => RoleName.ADMIN_L2.ToString(),
            3 => RoleName.ADMIN_L3.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };

        // Ensure only one admin level is present at a time.
        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        if (user is null)
            throw new InvalidOperationException("user_not_found");

        var roles = user.Role ?? new List<string> { "USER" };
        var adminRoles = new[]
        {
            RoleName.ADMIN_L1.ToString(),
            RoleName.ADMIN_L2.ToString(),
            RoleName.ADMIN_L3.ToString(),
        };

        // Remove any other admin levels
        var removed =
            roles.RemoveAll(r =>
                adminRoles.Any(ar => string.Equals(ar, r, StringComparison.OrdinalIgnoreCase))
            ) > 0;

        // Add target admin level if not present
        var added = false;
        if (!roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add(roleName);
            added = true;
        }

        // Ensure we always have USER
        if (!roles.Any())
            roles.Add("USER");

        // If underlying raw column not set, mark as changed to persist
        if (string.IsNullOrWhiteSpace(user.RoleRaw))
            removed = true;

        if (removed || added)
        {
            user.Role = roles;
            await _userRepository.UpdateUserAsync(user, ct);
        }
    }

    public async Task DemoteFromRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken ct = default
    )
    {
        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        if (user is null)
            throw new InvalidOperationException("user_not_found");

        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("invalid_role", nameof(roleName));

        if (IsProtectedRole(roleName))
            throw new InvalidOperationException("cannot_remove_protected_role");

        var roles = user.Role ?? new List<string> { "USER" };
        var removed =
            roles.RemoveAll(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase))
            > 0;
        if (removed)
        {
            if (!roles.Any())
                roles.Add("USER");
            user.Role = roles;
            await _userRepository.UpdateUserAsync(user, ct);
        }
    }

    public async Task DemoteAdminLevelAsync(Guid userId, int level, CancellationToken ct = default)
    {
        if (level < 1 || level > 3)
            throw new ArgumentOutOfRangeException(nameof(level));

        var roleName = level switch
        {
            1 => RoleName.ADMIN_L1.ToString(),
            2 => RoleName.ADMIN_L2.ToString(),
            3 => RoleName.ADMIN_L3.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };

        await DemoteFromRoleAsync(userId, roleName, ct);
    }

    public async Task ToggleRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        var user = await _userRepository.GetUserWithPersonalDataAsync(userId, ct);
        if (user is null)
            throw new InvalidOperationException("user_not_found");

        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("invalid_role", nameof(roleName));

        var roles = user.Role ?? new List<string> { "USER" };

        // Special-case: toggling "USER" should demote to plain user — remove all admin roles
        var adminRoles = new[]
        {
            RoleName.ADMIN_L1.ToString(),
            RoleName.ADMIN_L2.ToString(),
            RoleName.ADMIN_L3.ToString(),
        };
        if (string.Equals(roleName, "USER", StringComparison.OrdinalIgnoreCase))
        {
            // remove admin levels
            var removedAdmins =
                roles.RemoveAll(r =>
                    adminRoles.Any(ar => string.Equals(ar, r, StringComparison.OrdinalIgnoreCase))
                ) > 0;
            // leave only USER
            roles.RemoveAll(r => !string.Equals(r, "USER", StringComparison.OrdinalIgnoreCase));
            if (!roles.Any())
                roles.Add("USER");

            if (removedAdmins || string.IsNullOrWhiteSpace(user.RoleRaw))
            {
                user.Role = roles;
                await _userRepository.UpdateUserAsync(user, ct);
            }

            return;
        }

        var hasRole = roles.Any(r =>
            string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase)
        );
        if (hasRole)
        {
            return;
        }

        // Not present -> promote. If it's an admin level, ensure uniqueness.
        if (adminRoles.Any(ar => string.Equals(ar, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            // remove other admin levels
            roles.RemoveAll(r =>
                adminRoles.Any(ar => string.Equals(ar, r, StringComparison.OrdinalIgnoreCase))
            );
            roles.Add(roleName);
        }
        else
        {
            roles.Add(roleName);
        }

        if (!roles.Any())
            roles.Add("USER");

        user.Role = roles;
        await _userRepository.UpdateUserAsync(user, ct);
    }
}
