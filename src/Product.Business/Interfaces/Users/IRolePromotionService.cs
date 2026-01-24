using System.Threading;

namespace Product.Business.Interfaces.Users;

public interface IRolePromotionService
{
    /// <summary>
    /// Atribui um papel a um usuário se ainda não possuir.
    /// </summary>
    Task PromoteToRoleAsync(Guid userId, string roleName, CancellationToken ct = default);

    /// <summary>
    /// Promove o usuário a um nível de admin (1..3).
    /// </summary>
    Task PromoteToAdminLevelAsync(Guid userId, int level, CancellationToken ct = default);

    /// <summary>
    /// Remove a role from a user if present.
    /// </summary>
    Task DemoteFromRoleAsync(Guid userId, string roleName, CancellationToken ct = default);

    /// <summary>
    /// Demote user from an admin level (1..3) by removing the corresponding admin role.
    /// </summary>
    Task DemoteAdminLevelAsync(Guid userId, int level, CancellationToken ct = default);

    Task ToggleRoleAsync(Guid userId, string roleName, CancellationToken ct = default);
}
