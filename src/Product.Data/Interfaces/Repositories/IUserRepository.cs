using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Data.Interfaces.Repositories;

public interface IUserRepository
{
    Task<ApplicationUser?> GetUserWithPersonalDataAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<ApplicationUser?> GetUserWithPersonalDataByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    );
    Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<ApplicationUser?> GetUserByEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default
    );
    Task<bool> IsEmailTakenAsync(
        Guid userId,
        string normalizedEmail,
        CancellationToken ct = default
    );
    Task<bool> IsUsernameTakenAsync(
        Guid userId,
        string normalizedUsername,
        CancellationToken ct = default
    );
    Task<bool> IsCpfTakenAsync(string cpf, Guid userId, CancellationToken ct = default);
    Task<bool> UserNameExistsAsync(string normalizedUsername, CancellationToken ct = default);
    Task EnsurePersonalDataAsync(Guid userId, CancellationToken ct = default);
    Task AddUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task<string[]> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<RefreshToken?> GetRefreshTokenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default
    );
    Task<(IReadOnlyCollection<ApplicationUser> Users, int Total)> SearchUsersAsync(
        string? query,
        string? by,
        bool startsWith,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task UpdateUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
