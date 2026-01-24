namespace Product.Data.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<bool> RoleExistsAsync(string roleName, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
