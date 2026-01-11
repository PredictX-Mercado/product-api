using Microsoft.Extensions.Configuration;

namespace Product.Business.Interfaces.Users;

public interface IDatabaseSeeder
{
    Task SeedAsync(IConfiguration configuration, CancellationToken ct = default);
}
